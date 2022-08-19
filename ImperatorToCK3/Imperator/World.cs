﻿using commonItems;
using commonItems.Localization;
using commonItems.Mods;
using ImperatorToCK3.Imperator.Armies;
using ImperatorToCK3.Imperator.Characters;
using ImperatorToCK3.Imperator.Countries;
using ImperatorToCK3.Imperator.Families;
using ImperatorToCK3.Imperator.Genes;
using ImperatorToCK3.Imperator.Pops;
using ImperatorToCK3.Imperator.Provinces;
using ImperatorToCK3.Imperator.Religions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mods = System.Collections.Generic.List<commonItems.Mods.Mod>;
using Parser = commonItems.Parser;

namespace ImperatorToCK3.Imperator {
	public class World : Parser {
		private readonly Date startDate = new("450.10.1", AUC: true);
		public Date EndDate { get; private set; } = new Date("727.2.17", AUC: true);
		private GameVersion imperatorVersion = new();
		public ModFilesystem ModFS { get; private set; }
		private readonly SortedSet<string> dlcs = new();
		private readonly ScriptValueCollection scriptValues = new();
		public Defines Defines { get; }= new();
		public LocDB LocDB { get; } = new("english", "french", "german", "russian", "simp_chinese", "spanish");

		public NamedColorCollection NamedColors { get; } = new();
		public FamilyCollection Families { get; private set; } = new();
		public CharacterCollection Characters { get; private set; } = new();
		private PopCollection pops = new();
		public ProvinceCollection Provinces { get; private set; } = new();
		public CountryCollection Countries { get; private set; } = new();
		public Jobs.Jobs Jobs { get; private set; } = new();
		public UnitCollection Units { get; private set; } = new();
		public ReligionCollection Religions { get; private set; }
		private GenesDB genesDB = new();

		private enum SaveType { Invalid, Plaintext, CompressedEncoded }
		private SaveType saveType = SaveType.Invalid;

		public World(Configuration config) {
			ModFS = new ModFilesystem(Path.Combine(config.ImperatorPath, "game"), new Mod[] { });
			Religions = new ReligionCollection(new ScriptValueCollection());
		}
		public World(Configuration config, ConverterVersion converterVersion): this(config) {
			Logger.Info("*** Hello Imperator, Roma Invicta! ***");

			var imperatorRoot = Path.Combine(config.ImperatorPath, "game");
			
			ParseGenes(config);

			// Parse the save.
			RegisterRegex(@"\bSAV\w*\b", _ => { });
			RegisterKeyword("version", reader => {
				var versionString = reader.GetString();
				imperatorVersion = new GameVersion(versionString);
				Logger.Info($"Save game version: {versionString}");

				if (converterVersion.MinSource > imperatorVersion) {
					Logger.Error(
						$"Converter requires a minimum save from v{converterVersion.MinSource.ToShortString()}");
					throw new FormatException("Save game vs converter version mismatch!");
				}
				if (!converterVersion.MaxSource.IsLargerishThan(imperatorVersion)) {
					Logger.Error(
						$"Converter requires a maximum save from v{converterVersion.MaxSource.ToShortString()}");
					throw new FormatException("Save game vs converter version mismatch!");
				}
			});
			RegisterKeyword("date", reader => {
				var dateString = reader.GetString();
				EndDate = new Date(dateString, AUC: true);  // converted to AD
				Logger.Info($"Date: {dateString} AUC ({EndDate} AD)");
			});
			RegisterKeyword("enabled_dlcs", reader => {
				dlcs.UnionWith(reader.GetStrings());
				foreach (var dlc in dlcs) {
					Logger.Info($"Enabled DLC: {dlc}");
				}
				Logger.IncrementProgress();
			});
			RegisterKeyword("enabled_mods", reader => {
				Logger.Info("Detecting used mods...");
				var modsList = reader.GetStrings();
				Logger.Info($"Save game claims {modsList.Count} mods used:");
				Mods incomingMods = new();
				foreach (var modPath in modsList) {
					Logger.Info($"Used mod: {modPath}");
					incomingMods.Add(new Mod(string.Empty, modPath));
				}
				Logger.IncrementProgress();

				// Let's locate, verify and potentially update those mods immediately.
				ModLoader modLoader = new();
				modLoader.LoadMods(config.ImperatorDocPath, incomingMods);
				ModFS = new ModFilesystem(imperatorRoot, modLoader.UsableMods);
				
				// Now that we have the list of mods used, we can load data from Imperator mod filesystem
				LoadModFilesystemDependentData();
			});
			RegisterKeyword("family", reader => {
				Logger.Info("Loading Families...");
				Families = FamilyCollection.ParseBloc(reader);
				Logger.Info($"Loaded {Families.Count} families.");
				Logger.IncrementProgress();
			});
			RegisterKeyword("character", reader => {
				Logger.Info("Loading Characters...");
				Characters = CharacterCollection.ParseBloc(reader, genesDB);
				Logger.Info($"Loaded {Characters.Count} characters.");
				Logger.IncrementProgress();
			});
			RegisterKeyword("provinces", reader => {
				Logger.Info("Loading Provinces...");
				Provinces = new ProvinceCollection(reader);
				Logger.Debug($"Ignored Province tokens: {string.Join(", ", Province.IgnoredTokens)}");
				Logger.Info($"Loaded {Provinces.Count} provinces.");
			});
			RegisterKeyword("armies", reader => {
				Logger.Info("Loading armies...");
				var armiesParser = new Parser();
				armiesParser.RegisterKeyword("subunit_database", subunitsReader => Units.LoadSubunits(subunitsReader));
				armiesParser.RegisterKeyword("units_database", unitsReader => Units.LoadUnits(unitsReader, LocDB, Defines));

				armiesParser.ParseStream(reader);
			});
			RegisterKeyword("country", reader => {
				Logger.Info("Loading Countries...");
				Countries = CountryCollection.ParseBloc(reader);
				Logger.Info($"Loaded {Countries.Count} countries.");
				Logger.IncrementProgress();
			});
			RegisterKeyword("population", reader => {
				Logger.Info("Loading Pops...");
				pops = PopCollection.ParseBloc(reader);
				Logger.Info($"Loaded {pops.Count} pops.");
				Logger.IncrementProgress();
			});
			RegisterKeyword("jobs", reader => {
				Logger.Info("Loading Jobs...");
				Jobs = new Jobs.Jobs(reader);
				Logger.Info($"Loaded {Jobs.Governorships.Capacity} governorships.");
				Logger.IncrementProgress();
			});
			RegisterKeyword("deity_manager", reader => {
				Religions!.LoadHolySiteDatabase(reader);
			});
			RegisterKeyword("played_country", reader => {
				var playerCountriesToLog = new List<string>();
				var playedCountryBlocParser = new Parser();
				playedCountryBlocParser.RegisterKeyword("country", reader => {
					var countryId = reader.GetULong();
					var country = Countries[countryId];
					country.PlayerCountry = true;
					playerCountriesToLog.Add(country.Tag);
				});
				playedCountryBlocParser.RegisterRegex(CommonRegexes.Catchall, ParserHelpers.IgnoreItem);
				playedCountryBlocParser.ParseStream(reader);
				Logger.Info($"Player countries: {string.Join(", ", playerCountriesToLog)}");
				Logger.IncrementProgress();
			});
			this.IgnoreAndStoreUnregisteredItems(ignoredTokens);

			Logger.Info("Verifying Imperator save...");
			VerifySave(config.SaveGamePath);
			Logger.IncrementProgress();

			ParseStream(ProcessSave(config.SaveGamePath));
			ClearRegisteredRules();
			Logger.Debug($"Ignored World tokens: {string.Join(", ", ignoredTokens)}");

			Logger.Info("*** Building World ***");

			// Link all the intertwining references
			Logger.Info("Linking Characters with Families...");
			Characters.LinkFamilies(Families);
			Families.RemoveUnlinkedMembers();
			Logger.Info("Linking Characters with Countries...");
			Characters.LinkCountries(Countries);
			Logger.Info("Linking Provinces with Pops...");
			Provinces.LinkPops(pops);
			Logger.Info("Linking Provinces with Countries...");
			Provinces.LinkCountries(Countries);
			Logger.Info("Linking Countries with Families...");
			Countries.LinkFamilies(Families);
			
			LoadPreImperatorRulers();

			Logger.Info("*** Good-bye Imperator, rest in peace. ***");
		}
		private void ParseGenes(Configuration config) {
			genesDB = new GenesDB(Path.Combine(config.ImperatorPath, "game/common/genes/00_genes.txt"));
		}
		private void LoadPreImperatorRulers() {
			const string filePath = "configurables/prehistory.txt";
			const string noRulerWarning = "Pre-Imperator ruler term has no pre-Imperator ruler!";
			const string noCountryIdWarning = "Pre-Imperator ruler term has no country ID!";

			var preImperatorRulerTerms = new Dictionary<ulong, List<RulerTerm>>(); // <country id, list of terms>
			var parser = new Parser();
			parser.RegisterKeyword("ruler", reader => {
				var rulerTerm = new RulerTerm(reader, Countries);
				if (rulerTerm.PreImperatorRuler is null) {
					Logger.Warn(noRulerWarning);
					return;
				}
				if (rulerTerm.PreImperatorRuler.Country is null) {
					Logger.Warn(noCountryIdWarning);
					return;
				}
				var countryId = rulerTerm.PreImperatorRuler.Country.Id;
				Countries[countryId].RulerTerms.Add(rulerTerm);
				if (preImperatorRulerTerms.TryGetValue(countryId, out var list)) {
					list.Add(rulerTerm);
				} else {
					preImperatorRulerTerms[countryId] = new() { rulerTerm };
				}
			});
			parser.RegisterRegex(CommonRegexes.Catchall, ParserHelpers.IgnoreAndLogItem);
			parser.ParseFile(filePath);

			foreach (var country in Countries) {
				country.RulerTerms = country.RulerTerms.OrderBy(t => t.StartDate).ToList();
			}

			// verify with data from historical_regnal_numbers
			var regnalNameCounts = new Dictionary<ulong, Dictionary<string, int>>(); // <country id, <name, count>>
			foreach (var country in Countries) {
				if (!preImperatorRulerTerms.ContainsKey(country.Id)) {
					continue;
				}

				regnalNameCounts.Add(country.Id, new());
				var countryRulerTerms = regnalNameCounts[country.Id];

				foreach (var term in preImperatorRulerTerms[country.Id]) {
					if (term.PreImperatorRuler is null) {
						Logger.Warn(noRulerWarning);
						continue;
					}
					var name = term.PreImperatorRuler.Name;
					if (name is null) {
						Logger.Warn("Pre-Imperator ruler has no country name!");
						continue;
					}
					if (countryRulerTerms.ContainsKey(name)) {
						++countryRulerTerms[name];
					} else {
						countryRulerTerms[name] = 1;
					}
				}
			}
			foreach (var country in Countries) {
				bool equal;
				if (!regnalNameCounts.ContainsKey(country.Id)) {
					equal = country.HistoricalRegnalNumbers.Count == 0;
				} else {
					equal = country.HistoricalRegnalNumbers.OrderBy(kvp => kvp.Key)
						.SequenceEqual(regnalNameCounts[country.Id].OrderBy(kvp => kvp.Key)
					);
				}

				if (!equal) {
					Logger.Debug($"List of pre-Imperator rulers of {country.Tag} doesn't match data from save!");
				}
			}
		}

		private void LoadModFilesystemDependentData() {
			scriptValues.LoadScriptValues(ModFS);
			Logger.IncrementProgress();
			Defines.LoadDefines(ModFS);
			NamedColors.LoadNamedColors("common/named_colors", ModFS);
			
			Country.LoadGovernments(ModFS);
				
			Religions = new ReligionCollection(scriptValues);
			Religions.LoadDeities(ModFS);
			Religions.LoadReligions(ModFS);
			
			LocDB.ScrapeLocalizations(ModFS);
			Logger.IncrementProgress();
		}

		private BufferedReader ProcessSave(string saveGamePath) {
			switch (saveType) {
				case SaveType.Plaintext:
					Logger.Info("Importing debug_mode Imperator save.");
					return ProcessDebugModeSave(saveGamePath);
				case SaveType.CompressedEncoded:
					Logger.Info("Importing regular Imperator save.");
					return ProcessCompressedEncodedSave(saveGamePath);
				default:
					throw new InvalidDataException("Unknown save type.");
			}
		}
		private void VerifySave(string saveGamePath) {
			using var saveStream = File.Open(saveGamePath, FileMode.Open);
			var buffer = new byte[10];
			saveStream.Read(buffer, 0, 4);
			if (buffer[0] != 'S' || buffer[1] != 'A' || buffer[2] != 'V') {
				throw new InvalidDataException("Save game of unknown type!");
			}

			char ch;
			do { // skip until newline
				ch = (char)saveStream.ReadByte();
			} while (ch != '\n' && ch != '\r');

			var length = saveStream.Length;
			if (length < 65536) {
				throw new InvalidDataException("Save game seems a bit too small.");
			}

			saveStream.Position = 0;
			var bigBuf = new byte[65536];
			var bytesReadCount = saveStream.Read(bigBuf);
			if (bytesReadCount < 65536) {
				throw new InvalidDataException($"Read only {bytesReadCount}bytes.");
			}
			saveType = SaveType.Plaintext;
			for (var i = 0; i < 65533; ++i) {
				if (BitConverter.ToUInt32(bigBuf, i) == 0x04034B50 && BitConverter.ToUInt16(bigBuf, i - 2) == 4) {
					saveType = SaveType.CompressedEncoded;
				}
			}
		}
		private static BufferedReader ProcessDebugModeSave(string saveGamePath) {
			return new BufferedReader(File.Open(saveGamePath, FileMode.Open));
		}
		private static BufferedReader ProcessCompressedEncodedSave(string saveGamePath) {
			Helpers.RakalyCaller.MeltSave(saveGamePath);
			return new BufferedReader(File.Open("temp/melted_save.rome", FileMode.Open));
		}

		private readonly HashSet<string> ignoredTokens = new();
	}
}
