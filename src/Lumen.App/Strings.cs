using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lumen.Core;

namespace Lumen.App;

public static class L
{
	private const string Table = "";

	private static readonly Dictionary<string, string[]> Rows = Parse();

	// Lumen is English only: the text keys are the English words.
	private static readonly string[] Codes = Array.Empty<string>();

	private static readonly Dictionary<string, string> Cultures = new Dictionary<string, string>
	{
		["en"] = "en-GB",
		["de"] = "de-CH",
		["fr"] = "fr-FR",
		["it"] = "it-IT",
		["es"] = "es-ES",
		["pt"] = "pt-PT"
	};

	public static readonly (string Code, string Name)[] Choices = new (string, string)[1] { ("en", "English") };

	public static string Language { get; private set; } = "en";

	public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-GB");

	public static IReadOnlyDictionary<string, string[]> All => Rows;

	private static Dictionary<string, string[]> Parse()
	{
		Dictionary<string, string[]> dictionary = new Dictionary<string, string[]>();
		string[] array = Table.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].TrimEnd('\r').Split('|');
			if (array2.Length == 6)
			{
				dictionary[array2[0]] = array2[1..];
			}
		}
		return dictionary;
	}

	public static void Use(string setting)
	{
		// English only, whatever was saved before.
		string text = "en";
		Language = text;
		Culture = CultureInfo.GetCultureInfo(Cultures[text]);
		Format.RpmUnit = text == "de" ? "U/min" : "rpm";
	}

	public static string T(string key)
	{
		int num = Array.IndexOf(Codes, Language);
		if (num < 0 || !Rows.TryGetValue(key, out string[] value) || value[num].Length <= 0)
		{
			return key;
		}
		return value[num];
	}

	public static string F(string key, params object[] args)
	{
		return string.Format(Culture, T(key), args);
	}
}






