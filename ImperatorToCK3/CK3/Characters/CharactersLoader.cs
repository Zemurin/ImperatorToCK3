﻿using commonItems;
using commonItems.Mods;
using Open.Collections.Synchronized;

namespace ImperatorToCK3.CK3.Characters;

public partial class CharacterCollection {
	public void LoadCK3Characters(ModFilesystem ck3ModFS) {
		Logger.Info("Loading characters from CK3...");

		var loadedCharacters = new ConcurrentList<Character>();
			
		var parser = new Parser();
		parser.RegisterRegex(CommonRegexes.String, (reader, characterId) => {
			var character = new Character(characterId, reader, this);
			
			// Check if character has a birth date:
			if (character.History.Fields["birth"].DateToEntriesDict.Count == 0) {
				Logger.Debug($"Ignoring character {characterId} with no valid birth date.");
				return;
			}
			
			AddOrReplace(character);
			loadedCharacters.Add(character);
		});
		parser.IgnoreAndLogUnregisteredItems();
		parser.ParseGameFolder("history/characters", ck3ModFS, "txt", recursive: true, parallel: true);
		
		foreach (var character in loadedCharacters) {
			character.UpdateChildrenCacheOfParents();
		}
	}
}