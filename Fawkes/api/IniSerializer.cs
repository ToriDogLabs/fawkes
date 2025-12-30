using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Tools
{
	/// <summary>
	/// Serializer class for ini files.
	/// </summary>
	public static class IniSerializer
	{
		public static IniDocument Deserialize(string serialized)
		{
			return Deserialize(serialized, new IniSerializerOptions());
		}

		public static IniDocument Deserialize(string serialized, IniSerializerOptions options)
		{
			var doc = new IniDocument(options.CaseSensitive);

			using var r = new StringReader(serialized);

			var currentSection = IniDocument.EmptySectionName;
			string? line;
			var lineNum = 0;

			while ((line = r.ReadLine()) != null)
			{
				lineNum++;
				var workingLine = line.Trim();

				if (workingLine.Length == 0)
				{
					//line with only spaces - ignore
					//continue;
				}
				else if (workingLine[0] == ';')
				{
					//comment - skip this line
					//continue;
				}
				else if (workingLine[0] == '[')
				{
					if (workingLine[^1] != ']')
					{
						throw new IniSerializerException($"No closing ] found on line {lineNum}");
					}

					//section
					var section = workingLine[1..^1];

					if (string.IsNullOrWhiteSpace(section))
					{
						throw new IniSerializerException("Section name empty on line {lineNum}");
					}

					currentSection = section.Trim();

					if (options.AllowEmptySections)
					{
						doc.Add(currentSection);
					}
				}
				else if (workingLine.Contains('='))
				{
					if (workingLine.LastIndexOf('=') != workingLine.IndexOf('='))
					{
						throw new IniSerializerException($"More than one '=' found on line {lineNum}");
					}

					// key value pair
					var pair = workingLine.Split('=');

					if (string.IsNullOrWhiteSpace(pair[0]))
					{
						throw new IniSerializerException($"Key empty on line {lineNum}");
					}

					doc.Add(currentSection, pair[0].Trim(), pair[1].Trim());
				}
				else if (workingLine.Contains(' '))
				{
					// key value pair with no equals
					var idx = workingLine.IndexOf(' ');
					var pair = new string[2];
					pair[0] = workingLine[0..(idx - 1)];
					pair[1] = workingLine[(idx + 1)..];

					if (string.IsNullOrWhiteSpace(pair[0]))
					{
						throw new IniSerializerException($"Key empty on line {lineNum}");
					}
					if (string.IsNullOrWhiteSpace(pair[1]))
					{
						throw new IniSerializerException($"Value empty on line {lineNum}");
					}

					doc.Add(currentSection, pair[0].Trim(), pair[1].Trim());
				}
				else
				{
					// throw new IniSerializerException($"Key with no value on line {lineNum}");
					doc.Add(currentSection, workingLine, "");
				}
			}

			return doc;
		}

		/// <summary>
		/// Serialize an <see cref="IniDocument" /> to a string using default options.
		/// </summary>
		/// <param name="document"></param>
		/// <returns></returns>
		public static string Serialize(this IniDocument document)
		{
			return Serialize(document, new IniSerializerOptions());
		}

		/// <summary>
		/// Serialize an <see cref="IniDocument" /> to a string using specified options.
		/// </summary>
		/// <param name="document"></param>
		/// <returns></returns>
		public static string Serialize(IniDocument document, IniSerializerOptions options)
		{
			ArgumentNullException.ThrowIfNull(document);

			var sb = new StringBuilder();
			var sections = document.Sections.ToList();

			for (var idx = 0; idx < sections.Count; idx++)
			{
				var section = sections[idx];

				if (options.AllowEmptySections || document[section].Keys.Count > 0)
				{
					if (idx != 0)
					{
						sb.AppendLine();
					}

					sb.AppendLine($"[{section}]");

					foreach (var kvp in document[section])
					{
						sb.AppendLine($"{kvp.Key} = {kvp.Value}");
					}
				}
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// Ini Document class
	/// </summary>
	public class IniDocument
	{
		public const string EmptySectionName = "default";

		private readonly Dictionary<string, Dictionary<string, string>> innerDic;

		public IniDocument(bool caseSensitive = false)
		{
			innerDic = new Dictionary<string, Dictionary<string, string>>(caseSensitive ? StringComparer.OrdinalIgnoreCase : default);
		}

		/// <summary>
		/// Return an enumerable list of section names.
		/// </summary>
		public IEnumerable<string> Sections => innerDic.Keys;

		/// <summary>
		/// Short cut to <see cref="IniDocument.Values" />.
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, string> this[string section] => innerDic[FormattedSectionName(section)];

		public static IniDocument Parse(string serializedIniDocument)
		{
			return IniSerializer.Deserialize(serializedIniDocument);
		}

		public static IniDocument? ParseFile(string path)
		{
			if (File.Exists(path))
			{
				return Parse(File.ReadAllText(path));
			}
			return null;
		}

		/// <summary>
		/// Add an empty section to the <see cref="IniDocument" />.
		/// </summary>
		/// <param name="section"></param>
		public void Add(string section)
		{
			innerDic.Add(FormattedSectionName(section), []);
		}

		/// <summary>
		/// Add a key value to the section in the <see cref="IniDocument" />. The section is created if it does not exist. EmptySectionName is used if
		/// no section name is provided.
		/// </summary>
		/// <param name="section"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		public void Add(string section, string key, string value)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException("key value cannot be empty", nameof(key));
			}

			var s = FormattedSectionName(section);

			if (!innerDic.ContainsKey(s))
			{
				Add(s);
			}

			innerDic[s].Add(key.Trim(), value);
		}

		public Dictionary<string, string> AddSection(string sectionName)
		{
			var section = new Dictionary<string, string>();
			innerDic.Add(sectionName, section);
			return section;
		}

		public Dictionary<string, string> GetOrAddSection(string sectionName)
		{
			var section = GetSection(sectionName);
			if (section == null)
			{
				return AddSection(sectionName);
			}
			return section;
		}

		public Dictionary<string, string>? GetSection(string section)
		{
			if (innerDic.TryGetValue(section, out var values))
			{
				return values;
			}
			return null;
		}

		public IReadOnlyCollection<string> GetSections()
		{
			return innerDic.Keys;
		}

		public void RemoveSection(string sectionName)
		{
			innerDic.Remove(sectionName);
		}

		public bool TryGetSection(string section, [NotNullWhen(true)] out Dictionary<string, string>? values)
		{
			return innerDic.TryGetValue(section, out values);
		}

		/// <summary>
		/// Get the value by providing section name and key name.
		/// </summary>
		/// <param name="section"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		public string Value(string section, string key) => innerDic[FormattedSectionName(section)][key];

		/// <summary>
		/// Get a Dictionary of key, value pairs for the section.
		/// </summary>
		/// <param name="section"></param>
		/// <returns></returns>
		public Dictionary<string, string> Values(string section) => innerDic[FormattedSectionName(section)];

		public async Task WriteToFile(string path)
		{
			var text = IniSerializer.Serialize(this);
			var directory = Path.GetDirectoryName(path);
			if (directory != null && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			await File.WriteAllTextAsync(path, text);
		}

		/// <summary>
		/// Used internally to make sure the section name is formatted properly or to use the default name if none is provided.
		/// </summary>
		/// <param name="section"></param>
		/// <returns></returns>
		private static string FormattedSectionName(string section) => string.IsNullOrWhiteSpace(section) ? EmptySectionName : section.Trim();
	}

	/// <summary>
	/// Exception thrown by the <see cref="IniSerializer" />.
	/// </summary>
	[System.Serializable]
	public class IniSerializerException : System.Exception
	{
		public IniSerializerException()
		{ }

		public IniSerializerException(string message) : base(message)
		{
		}

		public IniSerializerException(string message, System.Exception inner) : base(message, inner)
		{
		}
	}

	/// <summary>
	/// Options class to change serializer implementation
	/// </summary>
	public class IniSerializerOptions
	{
		/// <summary>
		/// Allow empty sections to be added. Default false.
		/// </summary>
		/// <value></value>
		public bool AllowEmptySections { get; set; } = false;

		/// <summary>
		/// Sections names and keys are case sensitive. Default false.
		/// </summary>
		/// <value></value>
		public bool CaseSensitive { get; set; } = false;
	}
}