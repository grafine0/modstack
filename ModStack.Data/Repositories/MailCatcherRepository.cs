using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;

namespace ModStack.Data.Repositories;

public class MailCatcherRepository : IMailCatcherRepository
{
    private readonly string _connectionString;

    public MailCatcherRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<InterceptedEmail>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<InterceptedEmail>(
            "SELECT * FROM InterceptedEmails ORDER BY ReceivedAt DESC");
    }

    public async Task<InterceptedEmail?> GetByIdAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<InterceptedEmail>(
            "SELECT * FROM InterceptedEmails WHERE Id = @Id", new { Id = id });
    }

    public async Task<long> InsertAsync(InterceptedEmail email)
    {
        using var connection = new SqliteConnection(_connectionString);
        const string sql = @"
            INSERT INTO InterceptedEmails (
                MessageId, Sender, Recipient, Subject, BodyHtml, BodyText, RawHeaders, ReceivedAt, IsRead
            ) VALUES (
                @MessageId, @Sender, @Recipient, @Subject, @BodyHtml, @BodyText, @RawHeaders, @ReceivedAt, @IsRead
            );
            SELECT last_insert_rowid();";
            
        return await connection.ExecuteScalarAsync<long>(sql, email);
    }

    public async Task DeleteAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "DELETE FROM InterceptedEmails WHERE Id = @Id", new { Id = id });
    }

    public async Task DeleteAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM InterceptedEmails");
    }

    public async Task MarkAsReadAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE InterceptedEmails SET IsRead = 1 WHERE Id = @Id", new { Id = id });
    }
}
