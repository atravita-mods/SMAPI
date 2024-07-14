using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using StardewModdingAPI.Internal;
using StardewModdingAPI.Toolkit;
using StardewModdingAPI.Toolkit.Serialization;
using StardewModdingAPI.Toolkit.Utilities;

namespace StardewModdingAPI.Framework;

internal sealed class TranslationsLoader
{
    private readonly ModToolkit Toolkit;

    internal TranslationsLoader(ModToolkit toolkit)
    {
        this.Toolkit = toolkit;
    }

    /// <summary>Reload translations for the given mods.</summary>
    /// <param name="mods">The mods for which to reload translations.</param>
    internal void ReloadTranslations(IEnumerable<IModMetadata> mods)
    {

        // mod translations
        foreach (IModMetadata metadata in mods)
        {
            this.ReloadModTranslation(metadata);
        }
    }

    internal void ReloadModTranslation(IModMetadata metadata)
    {
        // top-level mod
        {
            var translations = this.ReadTranslationFiles(Path.Combine(metadata.DirectoryPath, "i18n"), out IList<string> errors);
            if (errors.Any())
            {
                metadata.LogAsMod("Mod couldn't load some translation files:", LogLevel.Warn);
                foreach (string error in errors)
                    metadata.LogAsMod($"  - {error}", LogLevel.Warn);
            }

            metadata.Translations!.SetTranslations(translations);
        }

        // fake content packs
        foreach (ContentPack pack in metadata.GetFakeContentPacks())
            this.ReloadTranslationsForTemporaryContentPack(metadata, pack);
    }

    /// <summary>Load or reload translations for a temporary content pack created by a mod.</summary>
    /// <param name="parentMod">The parent mod which created the content pack.</param>
    /// <param name="contentPack">The content pack instance.</param>
    internal void ReloadTranslationsForTemporaryContentPack(IModMetadata parentMod, ContentPack contentPack)
    {
        var translations = this.ReadTranslationFiles(Path.Combine(contentPack.DirectoryPath, "i18n"), out IList<string> errors);
        if (errors.Any())
        {
            parentMod.LogAsMod($"Generated content pack at '{PathUtilities.GetRelativePath(Constants.ModsPath, contentPack.DirectoryPath)}' couldn't load some translation files:", LogLevel.Warn);
            foreach (string error in errors)
                parentMod.LogAsMod($"  - {error}", LogLevel.Warn);
        }

        contentPack.TranslationImpl.SetTranslations(translations);
    }

    /// <summary>Read translations from a directory containing JSON translation files.</summary>
    /// <param name="folderPath">The folder path to search.</param>
    /// <param name="errors">The errors indicating why translation files couldn't be parsed, indexed by translation filename.</param>
    internal IDictionary<string, IDictionary<string, string>> ReadTranslationFiles(string folderPath, out IList<string> errors)
    {
        JsonHelper jsonHelper = this.Toolkit.JsonHelper;

        // read translation files
        var translations = new Dictionary<string, IDictionary<string, string>>();
        errors = new List<string>();
        DirectoryInfo translationsDir = new(folderPath);
        if (translationsDir.Exists)
        {
            foreach (FileInfo file in translationsDir.EnumerateFiles("*.json"))
            {
                string locale = Path.GetFileNameWithoutExtension(file.Name.ToLower().Trim());
                try
                {
                    if (!jsonHelper.ReadJsonFileIfExists(file.FullName, out IDictionary<string, string>? data))
                    {
                        errors.Add($"{file.Name} file couldn't be read"); // mainly happens when the file is corrupted or empty
                        continue;
                    }

                    translations[locale] = data;
                }
                catch (Exception ex)
                {
                    errors.Add($"{file.Name} file couldn't be parsed: {ex.GetLogSummary()}");
                }
            }
        }

        // validate translations
        foreach (string locale in translations.Keys)
        {
            // handle duplicates
            HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> duplicateKeys = new(StringComparer.OrdinalIgnoreCase);
            foreach (string key in translations[locale].Keys)
            {
                if (!keys.Add(key))
                {
                    duplicateKeys.Add(key);
                    translations[locale].Remove(key);
                }
            }
            if (duplicateKeys.Any())
                errors.Add($"{locale}.json has duplicate translation keys: [{string.Join(", ", duplicateKeys)}]. Keys are case-insensitive.");
        }

        return translations;
    }
}
