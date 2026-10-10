using System.Globalization;
using AdhdScheduler.Core.Models;
using Microsoft.Data.Sqlite;

namespace AdhdScheduler.Core.Data;

/// <summary>
/// SQLite への保存と、1手のキュー操作をまとめたもの。
/// キューの順序（QueueOrder）は同一目標内の Queued な1手だけで 0 から連番に保つ。
/// </summary>
public sealed class AppStore
{
    private readonly string _connectionString;
    private readonly Func<DateTime> _now;

    public AppStore(string databasePath, Func<DateTime>? now = null)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        }.ToString();
        _now = now ?? (() => DateTime.Now);
    }

    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AdhdScheduler",
        "data.db");

    public void Initialize()
    {
        using var connection = Open();
        Execute(connection, null, """
            CREATE TABLE IF NOT EXISTS Goals (
                Id        TEXT PRIMARY KEY,
                Title     TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Steps (
                Id          TEXT PRIMARY KEY,
                GoalId      TEXT NOT NULL REFERENCES Goals(Id) ON DELETE CASCADE,
                Purpose     TEXT NOT NULL,
                Reason      TEXT NOT NULL,
                QueueOrder  INTEGER NOT NULL,
                Status      INTEGER NOT NULL,
                CreatedAt   TEXT NOT NULL,
                CompletedAt TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Steps_GoalId ON Steps(GoalId, Status, QueueOrder);
            CREATE TABLE IF NOT EXISTS LaterItems (
                Id        TEXT PRIMARY KEY,
                Text      TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS AppState (
                Id           INTEGER PRIMARY KEY CHECK (Id = 1),
                ActiveGoalId TEXT NULL
            );
            INSERT OR IGNORE INTO AppState (Id, ActiveGoalId) VALUES (1, NULL);
            """);
    }

    // ---- 目標 ----

    public IReadOnlyList<Goal> GetGoals()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, CreatedAt FROM Goals ORDER BY CreatedAt, rowid";
        using var reader = command.ExecuteReader();
        var goals = new List<Goal>();
        while (reader.Read())
        {
            goals.Add(new Goal
            {
                Id = Guid.Parse(reader.GetString(0)),
                Title = reader.GetString(1),
                CreatedAt = ParseDate(reader.GetString(2)),
            });
        }
        return goals;
    }

    public Goal AddGoal(string title)
    {
        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            Title = Require(title, nameof(title)),
            CreatedAt = _now(),
        };
        using var connection = Open();
        Execute(connection, null,
            "INSERT INTO Goals (Id, Title, CreatedAt) VALUES ($id, $title, $createdAt)",
            ("$id", goal.Id.ToString()),
            ("$title", goal.Title),
            ("$createdAt", FormatDate(goal.CreatedAt)));
        return goal;
    }

    public void RenameGoal(Guid goalId, string title)
    {
        var value = Require(title, nameof(title));
        using var connection = Open();
        Execute(connection, null,
            "UPDATE Goals SET Title = $title WHERE Id = $id",
            ("$title", value),
            ("$id", goalId.ToString()));
    }

    public void DeleteGoal(Guid goalId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var id = goalId.ToString();
        Execute(connection, transaction, "DELETE FROM Steps WHERE GoalId = $id", ("$id", id));
        Execute(connection, transaction, "DELETE FROM Goals WHERE Id = $id", ("$id", id));
        Execute(connection, transaction,
            "UPDATE AppState SET ActiveGoalId = NULL WHERE Id = 1 AND ActiveGoalId = $id",
            ("$id", id));
        transaction.Commit();
    }

    public Guid? GetActiveGoalId()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ActiveGoalId FROM AppState WHERE Id = 1";
        return command.ExecuteScalar() is string value ? Guid.Parse(value) : null;
    }

    public void SetActiveGoal(Guid? goalId)
    {
        using var connection = Open();
        Execute(connection, null,
            "UPDATE AppState SET ActiveGoalId = $id WHERE Id = 1",
            ("$id", goalId?.ToString()));
    }

    // ---- 1手 ----

    /// <summary>取り組む目標で Queued のうち QueueOrder 最小の1手。</summary>
    public Step? GetNextStep()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {StepColumns} FROM Steps
            WHERE GoalId = (SELECT ActiveGoalId FROM AppState WHERE Id = 1)
              AND Status = {(int)StepStatus.Queued}
            ORDER BY QueueOrder
            LIMIT 1
            """;
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadStep(reader) : null;
    }

    public Step? GetStep(Guid stepId)
    {
        using var connection = Open();
        return GetStep(connection, null, stepId);
    }

    public IReadOnlyList<Step> GetQueuedSteps(Guid goalId)
    {
        using var connection = Open();
        return GetQueuedSteps(connection, null, goalId);
    }

    /// <summary>目標のキュー末尾に1手を追加する。</summary>
    public Step AppendStep(Guid goalId, string purpose, string reason)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var step = AppendStep(connection, transaction, goalId, purpose, reason);
        transaction.Commit();
        return step;
    }

    /// <summary>指定した1手の直前に新しい1手を割り込ませる（前段階追加）。</summary>
    public Step InsertBefore(Guid stepId, string purpose, string reason)
    {
        var purposeValue = Require(purpose, nameof(purpose));
        var reasonValue = Require(reason, nameof(reason));

        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var target = GetStep(connection, transaction, stepId)
            ?? throw new InvalidOperationException("指定された1手が見つかりません。");
        if (target.Status != StepStatus.Queued)
        {
            throw new InvalidOperationException("完了済みの1手の前には追加できません。");
        }

        var ids = GetQueuedSteps(connection, transaction, target.GoalId).Select(s => s.Id).ToList();
        var step = NewStep(target.GoalId, purposeValue, reasonValue);
        InsertStep(connection, transaction, step);
        step.QueueOrder = ids.IndexOf(stepId);
        ids.Insert(step.QueueOrder, step.Id);
        Renumber(connection, transaction, ids);
        transaction.Commit();
        return step;
    }

    public void UpdateStep(Guid stepId, string purpose, string reason)
    {
        var purposeValue = Require(purpose, nameof(purpose));
        var reasonValue = Require(reason, nameof(reason));
        using var connection = Open();
        Execute(connection, null,
            "UPDATE Steps SET Purpose = $purpose, Reason = $reason WHERE Id = $id",
            ("$purpose", purposeValue),
            ("$reason", reasonValue),
            ("$id", stepId.ToString()));
    }

    public void DeleteStep(Guid stepId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var step = GetStep(connection, transaction, stepId);
        if (step is null)
        {
            return;
        }
        Execute(connection, transaction, "DELETE FROM Steps WHERE Id = $id", ("$id", stepId.ToString()));
        var ids = GetQueuedSteps(connection, transaction, step.GoalId).Select(s => s.Id).ToList();
        Renumber(connection, transaction, ids);
        transaction.Commit();
    }

    /// <summary>キュー内で1手を offset だけ移動する。移動できなければ false。</summary>
    public bool MoveStep(Guid stepId, int offset)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var step = GetStep(connection, transaction, stepId);
        if (step is null || step.Status != StepStatus.Queued)
        {
            return false;
        }
        var ids = GetQueuedSteps(connection, transaction, step.GoalId).Select(s => s.Id).ToList();
        var from = ids.IndexOf(stepId);
        var to = from + offset;
        if (to < 0 || to >= ids.Count || to == from)
        {
            return false;
        }
        ids.RemoveAt(from);
        ids.Insert(to, stepId);
        Renumber(connection, transaction, ids);
        transaction.Commit();
        return true;
    }

    public void CompleteStep(Guid stepId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var step = GetStep(connection, transaction, stepId);
        if (step is null || step.Status != StepStatus.Queued)
        {
            return;
        }
        Execute(connection, transaction,
            "UPDATE Steps SET Status = $status, CompletedAt = $completedAt WHERE Id = $id",
            ("$status", (int)StepStatus.Done),
            ("$completedAt", FormatDate(_now())),
            ("$id", stepId.ToString()));
        var ids = GetQueuedSteps(connection, transaction, step.GoalId).Select(s => s.Id).ToList();
        Renumber(connection, transaction, ids);
        transaction.Commit();
    }

    // ---- 後でやる ----

    /// <summary>メモを保存する。空白のみなら保存せず null を返す。</summary>
    public LaterItem? AddLaterItem(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var item = new LaterItem
        {
            Id = Guid.NewGuid(),
            Text = text.Trim(),
            CreatedAt = _now(),
        };
        using var connection = Open();
        Execute(connection, null,
            "INSERT INTO LaterItems (Id, Text, CreatedAt) VALUES ($id, $text, $createdAt)",
            ("$id", item.Id.ToString()),
            ("$text", item.Text),
            ("$createdAt", FormatDate(item.CreatedAt)));
        return item;
    }

    /// <summary>古い順。</summary>
    public IReadOnlyList<LaterItem> GetLaterItems()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Text, CreatedAt FROM LaterItems ORDER BY CreatedAt, rowid";
        using var reader = command.ExecuteReader();
        var items = new List<LaterItem>();
        while (reader.Read())
        {
            items.Add(new LaterItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                Text = reader.GetString(1),
                CreatedAt = ParseDate(reader.GetString(2)),
            });
        }
        return items;
    }

    public void DeleteLaterItem(Guid itemId)
    {
        using var connection = Open();
        Execute(connection, null, "DELETE FROM LaterItems WHERE Id = $id", ("$id", itemId.ToString()));
    }

    /// <summary>メモを目標のキュー末尾に1手として追加し、メモを削除する。</summary>
    public Step TaskifyLaterItem(Guid itemId, Guid goalId, string purpose, string reason)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var step = AppendStep(connection, transaction, goalId, purpose, reason);
        Execute(connection, transaction, "DELETE FROM LaterItems WHERE Id = $id", ("$id", itemId.ToString()));
        transaction.Commit();
        return step;
    }

    // ---- 内部 ----

    private const string StepColumns = "Id, GoalId, Purpose, Reason, QueueOrder, Status, CreatedAt, CompletedAt";

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        Execute(connection, null, "PRAGMA foreign_keys = ON");
        return connection;
    }

    private Step AppendStep(SqliteConnection connection, SqliteTransaction transaction, Guid goalId, string purpose, string reason)
    {
        var step = NewStep(goalId, Require(purpose, nameof(purpose)), Require(reason, nameof(reason)));
        var ids = GetQueuedSteps(connection, transaction, goalId).Select(s => s.Id).ToList();
        InsertStep(connection, transaction, step);
        ids.Add(step.Id);
        Renumber(connection, transaction, ids);
        step.QueueOrder = ids.Count - 1;
        return step;
    }

    private Step NewStep(Guid goalId, string purpose, string reason) => new()
    {
        Id = Guid.NewGuid(),
        GoalId = goalId,
        Purpose = purpose,
        Reason = reason,
        Status = StepStatus.Queued,
        CreatedAt = _now(),
    };

    private static void InsertStep(SqliteConnection connection, SqliteTransaction transaction, Step step)
    {
        Execute(connection, transaction, $"""
            INSERT INTO Steps ({StepColumns})
            VALUES ($id, $goalId, $purpose, $reason, $queueOrder, $status, $createdAt, NULL)
            """,
            ("$id", step.Id.ToString()),
            ("$goalId", step.GoalId.ToString()),
            ("$purpose", step.Purpose),
            ("$reason", step.Reason),
            ("$queueOrder", step.QueueOrder),
            ("$status", (int)step.Status),
            ("$createdAt", FormatDate(step.CreatedAt)));
    }

    /// <summary>与えた順に QueueOrder を 0 から振り直す。</summary>
    private static void Renumber(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Guid> orderedIds)
    {
        for (var i = 0; i < orderedIds.Count; i++)
        {
            Execute(connection, transaction,
                "UPDATE Steps SET QueueOrder = $order WHERE Id = $id",
                ("$order", i),
                ("$id", orderedIds[i].ToString()));
        }
    }

    private static Step? GetStep(SqliteConnection connection, SqliteTransaction? transaction, Guid stepId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {StepColumns} FROM Steps WHERE Id = $id";
        command.Parameters.AddWithValue("$id", stepId.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadStep(reader) : null;
    }

    private static List<Step> GetQueuedSteps(SqliteConnection connection, SqliteTransaction? transaction, Guid goalId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {StepColumns} FROM Steps
            WHERE GoalId = $goalId AND Status = {(int)StepStatus.Queued}
            ORDER BY QueueOrder, CreatedAt, rowid
            """;
        command.Parameters.AddWithValue("$goalId", goalId.ToString());
        using var reader = command.ExecuteReader();
        var steps = new List<Step>();
        while (reader.Read())
        {
            steps.Add(ReadStep(reader));
        }
        return steps;
    }

    private static Step ReadStep(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        GoalId = Guid.Parse(reader.GetString(1)),
        Purpose = reader.GetString(2),
        Reason = reader.GetString(3),
        QueueOrder = reader.GetInt32(4),
        Status = (StepStatus)reader.GetInt32(5),
        CreatedAt = ParseDate(reader.GetString(6)),
        CompletedAt = reader.IsDBNull(7) ? null : ParseDate(reader.GetString(7)),
    };

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        command.ExecuteNonQuery();
    }

    private static string Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("空にはできません。", name);
        }
        return value.Trim();
    }

    private static string FormatDate(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
