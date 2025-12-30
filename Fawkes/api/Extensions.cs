namespace Fawkes.Api;

public static class Extensions
{
	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> o) where T : class
		=> o.Where(x => x != null)!;

	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> o) where T : struct
		=> o.Where(x => x.HasValue)!.Cast<T>();
}