namespace Fawkes.Api;

public static class SqlExtensions
{
	public static async Task ExecuteNonQueryAsync(this Npgsql.NpgsqlDataSource connection, string sql)
	{
		using var command = connection.CreateCommand(sql);
		await command.ExecuteNonQueryAsync();
	}

	public static async Task TryExecuteNonQueryAsync(this Npgsql.NpgsqlDataSource connection, string sql)
	{
		try
		{
			await connection.ExecuteNonQueryAsync(sql);
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
		}
	}
}