﻿using commonItems;
using commonItems.Collections;
using commonItems.Colors;
using commonItems.Localization;
using commonItems.Mods;
using ImperatorToCK3.CK3.Characters;
using ImperatorToCK3.CK3.Cultures;
using ImperatorToCK3.CK3.Provinces;
using ImperatorToCK3.CommonUtils;
using ImperatorToCK3.Imperator.Countries;
using ImperatorToCK3.Imperator.Jobs;
using ImperatorToCK3.Mappers.CoA;
using ImperatorToCK3.Mappers.Culture;
using ImperatorToCK3.Mappers.Government;
using ImperatorToCK3.Mappers.Nickname;
using ImperatorToCK3.Mappers.Province;
using ImperatorToCK3.Mappers.Region;
using ImperatorToCK3.Mappers.Religion;
using ImperatorToCK3.Mappers.SuccessionLaw;
using ImperatorToCK3.Mappers.TagTitle;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace ImperatorToCK3.CK3.Titles;

public partial class Title {
	private readonly LandedTitles parentCollection;

	// This is a recursive class that scrapes common/landed_titles looking for title colors, landlessness,
	// and most importantly relation between baronies and barony provinces so we can link titles to actual clay.
	// Since titles are nested according to hierarchy we do this recursively.
	public class LandedTitles : TitleCollection {
		public Dictionary<string, object> Variables { get; } = new();

		public void LoadTitles(ModFilesystem ck3ModFS) {
			Logger.Info("Loading landed titles...");

			var parser = new Parser();
			RegisterKeys(parser);

			var landedTitlesPath = Path.Combine("common", "landed_titles");
			parser.ParseGameFolder(landedTitlesPath, ck3ModFS, "txt", true);

			LogIgnoredTokens();

			Logger.IncrementProgress();
		}
		public void LoadTitles(BufferedReader reader) {
			var parser = new Parser();
			RegisterKeys(parser);
			parser.ParseStream(reader);

			LogIgnoredTokens();
		}
		public void LoadStaticTitles() {
			Logger.Info("Loading static landed titles...");

			var parser = new Parser();
			RegisterKeys(parser);

			parser.ParseFile("configurables/static_landed_titles.txt");

			LogIgnoredTokens();

			Logger.IncrementProgress();
		}
		public void LoadStaticTitles(BufferedReader reader) {
			Logger.Info("Loading static landed titles...");

			var parser = new Parser();
			RegisterKeys(parser);

			parser.ParseStream(reader);

			LogIgnoredTokens();

			Logger.IncrementProgress();
		}

		public void CarveTitles(LandedTitles overrides) {
			// merge in new king and empire titles into this from overrides, overriding duplicates
			foreach (var overrideTitle in overrides.Where(t => t.Rank > TitleRank.duchy)) {
				// inherit vanilla vassals
				TryGetValue(overrideTitle.Id, out Title? vanillaTitle);
				AddOrReplace(new Title(vanillaTitle, overrideTitle, this));
			}

			// update duchies to correct de jure liege, remove de jure titles that lose all de jure vassals
			foreach (var title in overrides.Where(t => t.Rank == TitleRank.duchy)) {
				var duchy = this[title.Id];
				if (duchy.DeJureLiege is not null) {
					if (duchy.DeJureLiege.DeJureVassals.Count <= 1) {
						duchy.DeJureLiege.DeJureLiege = null;
					}
				}
				duchy.DeJureLiege = title.DeJureLiege;
			}
		}

		private void RegisterKeys(Parser parser) {
			parser.RegisterRegex(CommonRegexes.Variable, (reader, variableName) => {
				var variableValue = reader.GetString();
				Variables[variableName[1..]] = variableValue;
			});
			parser.RegisterRegex(Regexes.TitleId, (reader, titleNameStr) => {
				// Pull the titles beneath this one and add them to the lot, overwriting existing ones.
				var newTitle = Add(titleNameStr);
				newTitle.LoadTitles(reader);
			});
			parser.IgnoreAndLogUnregisteredItems();
		}

		private static void LogIgnoredTokens() {
			Logger.Debug($"Ignored Title tokens: {Title.IgnoredTokens}");
		}

		public Title Add(string id) {
			if (string.IsNullOrEmpty(id)) {
				throw new ArgumentException("Not inserting a Title with empty id!");
			}

			var newTitle = new Title(this, id);
			dict[newTitle.Id] = newTitle;
			return newTitle;
		}

		public Title Add(
			Country country,
			CountryCollection imperatorCountries,
			LocDB locDB,
			ProvinceMapper provinceMapper,
			CoaMapper coaMapper,
			TagTitleMapper tagTitleMapper,
			GovernmentMapper governmentMapper,
			SuccessionLawMapper successionLawMapper,
			DefiniteFormMapper definiteFormMapper,
			ReligionMapper religionMapper,
			CultureMapper cultureMapper,
			NicknameMapper nicknameMapper,
			CharacterCollection characters,
			Date conversionDate,
			Configuration config
		) {
			var newTitle = new Title(this,
				country,
				imperatorCountries,
				locDB,
				provinceMapper,
				coaMapper,
				tagTitleMapper,
				governmentMapper,
				successionLawMapper,
				definiteFormMapper,
				religionMapper,
				cultureMapper,
				nicknameMapper,
				characters,
				conversionDate,
				config
			);
			dict[newTitle.Id] = newTitle;
			return newTitle;
		}

		public Title Add(
			string id,
			Governorship governorship,
			Country country,
			Imperator.Provinces.ProvinceCollection irProvinces,
			Imperator.Characters.CharacterCollection imperatorCharacters,
			bool regionHasMultipleGovernorships,
			bool staticDeJure,
			LocDB locDB,
			ProvinceMapper provinceMapper,
			CoaMapper coaMapper,
			DefiniteFormMapper definiteFormMapper,
			ImperatorRegionMapper imperatorRegionMapper
		) {
			var newTitle = new Title(this,
				id,
				governorship,
				country,
				irProvinces,
				imperatorCharacters,
				regionHasMultipleGovernorships,
				staticDeJure,
				locDB,
				provinceMapper,
				coaMapper,
				definiteFormMapper,
				imperatorRegionMapper
			);
			dict[newTitle.Id] = newTitle;
			return newTitle;
		}
		public override void Remove(string name) {
			if (dict.TryGetValue(name, out var titleToErase)) {
				var deJureLiege = titleToErase.DeJureLiege;
				deJureLiege?.DeJureVassals.Remove(name);

				foreach (var vassal in titleToErase.DeJureVassals) {
					vassal.DeJureLiege = null;
				}

				foreach (var title in this) {
					title.RemoveDeFactoLiegeReferences(name);
				}

				if (titleToErase.ImperatorCountry is not null) {
					titleToErase.ImperatorCountry.CK3Title = null;
				}
			}
			dict.Remove(name);
		}
		public Title? GetCountyForProvince(ulong provinceId) {
			foreach (var county in this.Where(title => title.Rank == TitleRank.county)) {
				if (county.CountyProvinces.Contains(provinceId)) {
					return county;
				}
			}
			return null;
		}

		public Title? GetBaronyForProvince(ulong provinceId) {
			var baronies = this.Where(title => title.Rank == TitleRank.barony);
			return baronies.FirstOrDefault(b => provinceId == b?.Province, defaultValue: null);
		}

		public HashSet<string> GetHolderIds(Date date) {
			return new HashSet<string>(this.Select(t => t.GetHolderId(date)));
		}
		public HashSet<string> GetAllHolderIds() {
			return this.SelectMany(t => t.GetAllHolderIds()).ToHashSet();
		}

		public void ImportImperatorCountries(
			CountryCollection imperatorCountries,
			TagTitleMapper tagTitleMapper,
			LocDB locDB,
			ProvinceMapper provinceMapper,
			CoaMapper coaMapper,
			GovernmentMapper governmentMapper,
			SuccessionLawMapper successionLawMapper,
			DefiniteFormMapper definiteFormMapper,
			ReligionMapper religionMapper,
			CultureMapper cultureMapper,
			NicknameMapper nicknameMapper,
			CharacterCollection characters,
			Date conversionDate,
			Configuration config
		) {
			Logger.Info("Importing Imperator Countries...");

			// landedTitles holds all titles imported from CK3. We'll now overwrite some and
			// add new ones from Imperator tags.
			var counter = 0;
			// We don't need pirates, barbarians etc.
			foreach (var country in imperatorCountries.Where(c => c.CountryType == CountryType.real)) {
				ImportImperatorCountry(
					country,
					imperatorCountries,
					tagTitleMapper,
					locDB,
					provinceMapper,
					coaMapper,
					governmentMapper,
					successionLawMapper,
					definiteFormMapper,
					religionMapper,
					cultureMapper,
					nicknameMapper,
					characters,
					conversionDate,
					config
				);
				++counter;
			}
			Logger.Info($"Imported {counter} countries from I:R.");
		}

		private void ImportImperatorCountry(
			Country country,
			CountryCollection imperatorCountries,
			TagTitleMapper tagTitleMapper,
			LocDB locDB,
			ProvinceMapper provinceMapper,
			CoaMapper coaMapper,
			GovernmentMapper governmentMapper,
			SuccessionLawMapper successionLawMapper,
			DefiniteFormMapper definiteFormMapper,
			ReligionMapper religionMapper,
			CultureMapper cultureMapper,
			NicknameMapper nicknameMapper,
			CharacterCollection characters,
			Date conversionDate,
			Configuration config
		) {
			// Create a new title or update existing title.
			var name = DetermineId(country, imperatorCountries, tagTitleMapper, locDB);

			if (TryGetValue(name, out var existingTitle)) {
				existingTitle.InitializeFromTag(
					country,
					imperatorCountries,
					locDB,
					provinceMapper,
					coaMapper,
					governmentMapper,
					successionLawMapper,
					definiteFormMapper,
					religionMapper,
					cultureMapper,
					nicknameMapper,
					characters,
					conversionDate,
					config
				);
			} else {
				Add(
					country,
					imperatorCountries,
					locDB,
					provinceMapper,
					coaMapper,
					tagTitleMapper,
					governmentMapper,
					successionLawMapper,
					definiteFormMapper,
					religionMapper,
					cultureMapper,
					nicknameMapper,
					characters,
					conversionDate,
					config
				);
			}
		}

		public void ImportImperatorGovernorships(
			Imperator.World impWorld,
			ProvinceCollection ck3Provinces,
			TagTitleMapper tagTitleMapper,
			LocDB locDB,
			Configuration config,
			ProvinceMapper provinceMapper,
			DefiniteFormMapper definiteFormMapper,
			ImperatorRegionMapper imperatorRegionMapper,
			CoaMapper coaMapper,
			List<Governorship> countryLevelGovernorships
		) {
			Logger.Info("Importing Imperator Governorships...");

			var governorships = impWorld.Jobs.Governorships;
			var imperatorCountries = impWorld.Countries;

			var governorshipsPerRegion = governorships.GroupBy(g => g.RegionName)
				.ToDictionary(g => g.Key, g => g.Count());

			// landedTitles holds all titles imported from CK3. We'll now overwrite some and
			// add new ones from Imperator governorships.
			var counter = 0;
			foreach (var governorship in governorships) {
				ImportImperatorGovernorship(
					governorship,
					imperatorCountries,
					this,
					ck3Provinces,
					impWorld.Provinces,
					impWorld.Characters,
					governorshipsPerRegion[governorship.RegionName] > 1,
					config.StaticDeJure,
					tagTitleMapper,
					locDB,
					provinceMapper,
					definiteFormMapper,
					imperatorRegionMapper,
					coaMapper,
					countryLevelGovernorships
				);
				++counter;
			}
			Logger.Info($"Imported {counter} governorships from I:R.");
			Logger.IncrementProgress();
		}
		private void ImportImperatorGovernorship(
			Governorship governorship,
			CountryCollection imperatorCountries,
			LandedTitles titles,
			ProvinceCollection ck3Provinces,
			Imperator.Provinces.ProvinceCollection irProvinces,
			Imperator.Characters.CharacterCollection imperatorCharacters,
			bool regionHasMultipleGovernorships,
			bool staticDeJure,
			TagTitleMapper tagTitleMapper,
			LocDB locDB,
			ProvinceMapper provinceMapper,
			DefiniteFormMapper definiteFormMapper,
			ImperatorRegionMapper imperatorRegionMapper,
			CoaMapper coaMapper,
			ICollection<Governorship> countryLevelGovernorships
		) {
			var country = imperatorCountries[governorship.CountryId];

			var id = DetermineId(governorship, country, titles, ck3Provinces, imperatorRegionMapper, tagTitleMapper);
			if (id is null) {
				Logger.Warn($"Cannot convert {governorship.RegionName} of country {country.Id}");
				return;
			}

			if (id.StartsWith("c_")) {
				countryLevelGovernorships.Add(governorship);
				return;
			}

			// Create a new title or update existing title
			if (TryGetValue(id, out var existingTitle)) {
				existingTitle.InitializeFromGovernorship(
					governorship,
					country,
					irProvinces,
					imperatorCharacters,
					regionHasMultipleGovernorships,
					staticDeJure,
					locDB,
					provinceMapper,
					definiteFormMapper,
					imperatorRegionMapper
				);
			} else {
				Add(
					id,
					governorship,
					country,
					irProvinces,
					imperatorCharacters,
					regionHasMultipleGovernorships,
					staticDeJure,
					locDB,
					provinceMapper,
					coaMapper,
					definiteFormMapper,
					imperatorRegionMapper
				);
			}
		}

		public void ImportImperatorHoldings(ProvinceCollection ck3Provinces, Imperator.Characters.CharacterCollection irCharacters, Date conversionDate) {
			Logger.Info("Importing Imperator holdings...");
			var counter = 0;
			
			var titlesThatHaveHolders = this
				.Where(t => t.Rank >= TitleRank.duchy && t.GetHolderId(conversionDate) != "0")
				.ToImmutableList();
			var titleCapitalBaronyIds = titlesThatHaveHolders
				.Select(t=>t.CapitalCounty?.CapitalBaronyId ?? t.CapitalBaronyId)
				.ToImmutableHashSet();
			
			// Dukes and above should be excluded from having their holdings converted.
			// Otherwise, governors with holdings would own parts of other governorships.
			var dukeAndAboveIds = titlesThatHaveHolders
				.Where(t => t.Rank >= TitleRank.duchy)
				.Select(t => t.GetHolderId(conversionDate))
				.ToImmutableHashSet();
			
			var baronies = this.Where(t => t.Rank == TitleRank.barony).ToImmutableHashSet();
			var countyCapitalBaronies = baronies
				.Where(b => b.DeJureLiege?.CapitalBaronyId == b.Id)
				.ToImmutableHashSet();
			
			var eligibleBaronies = baronies
				.Where(b => !titleCapitalBaronyIds.Contains(b.Id))
				.ToImmutableHashSet();

			foreach (var barony in eligibleBaronies) {
				var ck3ProvinceId = barony.Province;
				if (ck3ProvinceId is null) {
					continue;
				}
				if (!ck3Provinces.TryGetValue(ck3ProvinceId.Value, out var ck3Province)) {
					continue;
				}

				// Skip none holdings and temple holdings.
				if (ck3Province.GetHoldingType(conversionDate) is "church_holding" or "none") {
					continue;
				}

				var irProvince = ck3Province.PrimaryImperatorProvince;
				var holdingOwnerId = irProvince?.HoldingOwnerId;
				if (holdingOwnerId is null) {
					continue;
				}

				var irOwner = irCharacters[holdingOwnerId.Value];
				var ck3Owner = irOwner.CK3Character;
				if (ck3Owner is null) {
					continue;
				}
				if (dukeAndAboveIds.Contains(ck3Owner.Id)) {
					continue;
				}
				
				var realm = ck3Owner.ImperatorCharacter?.HomeCountry?.CK3Title;
				var deFactoLiege = realm;
				if (realm is not null) {
					var deJureDuchy = barony.DeJureLiege?.DeJureLiege;
					if (deJureDuchy is not null && deJureDuchy.GetHolderId(conversionDate) != "0" && deJureDuchy.GetTopRealm(conversionDate) == realm) {
						deFactoLiege = deJureDuchy;
					} else {
						var deJureKingdom = deJureDuchy?.DeJureLiege;
						if (deJureKingdom is not null && deJureKingdom.GetHolderId(conversionDate) != "0" && deJureKingdom.GetTopRealm(conversionDate) == realm) {
							deFactoLiege = deJureKingdom;
						}
					}
				}
				if (countyCapitalBaronies.Contains(barony)) {
					// If barony is a county capital, set the county holder to the holding owner.
					var county = barony.DeJureLiege;
					if (county is null) {
						Logger.Warn($"County capital barony {barony.Id} has no de jure county!");
						continue;
					}
					county.SetHolder(ck3Owner, conversionDate);
					county.SetDeFactoLiege(deFactoLiege, conversionDate);
				} else {
					barony.SetHolder(ck3Owner, conversionDate);
					// No need to set de facto liege for baronies, they are tied to counties.
				}
				++counter;
			}
			Logger.Info($"Imported {counter} holdings from I:R.");
			Logger.IncrementProgress();
		}

		public void RemoveInvalidLandlessTitles(Date ck3BookmarkDate) {
			Logger.Info("Removing invalid landless titles...");
			var removedGeneratedTitles = new HashSet<string>();
			var revokedVanillaTitles = new HashSet<string>();

			HashSet<string> countyHoldersCache = GetCountyHolderIds(ck3BookmarkDate);

			foreach (var title in this) {
				// If duchy/kingdom/empire title holder holds no counties, revoke the title.
				// In case of titles created from Imperator, completely remove them.
				if (title.Rank <= TitleRank.county) {
					continue;
				}
				if (countyHoldersCache.Contains(title.GetHolderId(ck3BookmarkDate))) {
					continue;
				}

				// Check if the title has "landless = yes" attribute.
				// If it does, it should be always kept.
				var id = title.Id;
				if (this[id].Landless) {
					continue;
				}

				if (title.IsCreatedFromImperator) {
					removedGeneratedTitles.Add(id);
					Remove(id);
				} else {
					revokedVanillaTitles.Add(id);
					title.ClearHolderSpecificHistory();
					title.SetDeFactoLiege(null, ck3BookmarkDate);
				}
			}
			if (removedGeneratedTitles.Count > 0) {
				Logger.Debug($"Found landless generated titles that can't be landless: {string.Join(", ", removedGeneratedTitles)}");
			}
			if (revokedVanillaTitles.Count > 0) {
				Logger.Debug($"Found landless vanilla titles that can't be landless: {string.Join(", ", revokedVanillaTitles)}");
			}

			Logger.IncrementProgress();
		}

		public void SetDeJureKingdomsAndEmpires(Date ck3BookmarkDate, ProvinceCollection ck3Provinces, CultureCollection ck3Cultures) {
			// Generate King/Empire de jure hierarchy from governorships
			Logger.Info("Setting de jure kingdoms...");
			foreach (var duchy in this.Where(t => t.Rank == TitleRank.duchy && t.DeJureVassals.Count > 0)) {
				// If capital county belongs to a kingdom, make the kingdom a de jure liege of the duchy.
				var capitalRealm = duchy.CapitalCounty?.GetRealmOfRank(TitleRank.kingdom, ck3BookmarkDate);
				if (capitalRealm is not null) {
					duchy.DeJureLiege = capitalRealm;
					continue;
				}

				// Otherwise, use the kingdom that owns the biggest percentage of the duchy.
				var kingdomRealmShares = new Dictionary<string, int>(); // realm, number of provinces held in duchy
				foreach (var county in duchy.GetDeJureVassalsAndBelow("c").Values) {
					var kingdomRealm = county.GetRealmOfRank(TitleRank.kingdom, ck3BookmarkDate);
					if (kingdomRealm is null) {
						continue;
					}
					kingdomRealmShares.TryGetValue(kingdomRealm.Id, out var currentCount);
					kingdomRealmShares[kingdomRealm.Id] = currentCount + county.CountyProvinces.Count();
				}
				if (kingdomRealmShares.Count > 0) {
					var biggestShare = kingdomRealmShares.MaxBy(pair => pair.Value);
					duchy.DeJureLiege = this[biggestShare.Key];
				}
			}
			Logger.IncrementProgress();

			Logger.Info("Setting de jure empires...");
			var deJureKingdoms = this
				.Where(t => t is {Rank: TitleRank.kingdom, DeJureVassals.Count: > 0})
				.ToImmutableArray();
			foreach (var kingdom in deJureKingdoms) {
				var empireShares = new Dictionary<string, int>();
				var kingdomProvincesCount = 0;
				foreach (var county in kingdom.GetDeJureVassalsAndBelow("c").Values) {
					var countyProvincesCount = county.CountyProvinces.Count();
					kingdomProvincesCount += countyProvincesCount;

					var empireRealm = county.GetRealmOfRank(TitleRank.empire, ck3BookmarkDate);
					if (empireRealm is null) {
						continue;
					}
					empireShares.TryGetValue(empireRealm.Id, out var currentCount);
					empireShares[empireRealm.Id] = currentCount + countyProvincesCount;
				}

				kingdom.DeJureLiege = null;
				if (empireShares.Count == 0) {
					continue;
				}
				(string empireId, int share) = empireShares.MaxBy(pair => pair.Value);
				// The potential de jure empire must hold at least 50% of the kingdom.
				if (share < (kingdomProvincesCount * 0.50)) {
					continue;
				}
				kingdom.DeJureLiege = this[empireId];
			}
			// For kingdoms that still have no de jure empire, create empires based on dominant cultural heritages.
			var kingdomsWithoutEmpire = deJureKingdoms
				.Where(k => k.DeJureLiege is null)
				.ToImmutableArray();
			var heritageToEmpireDict = new Dictionary<Pillar, Title>();
			foreach (var kingdom in kingdomsWithoutEmpire) {
				var counties = kingdom.GetDeJureVassalsAndBelow("c").Values;
				var kingdomProvinceIds = counties.SelectMany(c => c.CountyProvinces).ToImmutableHashSet();
				var kingdomProvinces = ck3Provinces.Where(p => kingdomProvinceIds.Contains(p.Id));
				var dominantHeritage = kingdomProvinces
					.Select(c => new { County = c, CultureId = c.GetCultureId(ck3BookmarkDate)})
					.Where(x => x.CultureId is not null)
					.Select(x => new { x.County, ck3Cultures[x.CultureId!].Heritage })
					.GroupBy(x => x.Heritage)
					.MaxBy(g => g.Count())?.Key;
				if (dominantHeritage is null) {
					if (kingdom.GetDeJureVassalsAndBelow("c").Count > 0) {
						Logger.Warn($"Kingdom {kingdom.Id} has no dominant heritage!");
					}
					continue;
				}
				if (heritageToEmpireDict.TryGetValue(dominantHeritage, out var empire)) {
					kingdom.DeJureLiege = empire;
				} else {
					// Create new de jure empire based on heritage.
					var newEmpireId = $"e_IRTOCK3_heritage_{dominantHeritage.Id}";
					var newEmpire = Add(newEmpireId);
					var nameLocBlock = newEmpire.Localizations.AddLocBlock(newEmpire.Id);
					nameLocBlock[ConverterGlobals.PrimaryLanguage] = $"${dominantHeritage.Id}_name$ Empire";
					var adjectiveLocBlock = newEmpire.Localizations.AddLocBlock($"{newEmpire.Id}_adj");
					adjectiveLocBlock[ConverterGlobals.PrimaryLanguage] = $"${dominantHeritage.Id}_name$";
					newEmpire.HasDefiniteForm = true;
					
					// Use color of one of the cultures as the empire color.
					var empireColor = ck3Cultures.First(c => c.Heritage == dominantHeritage).Color;
					newEmpire.Color1 = empireColor;
					
					kingdom.DeJureLiege = newEmpire;
					heritageToEmpireDict[dominantHeritage] = newEmpire;
				}
			}
			Logger.IncrementProgress();
		}

		private HashSet<string> GetCountyHolderIds(Date date) {
			var countyHoldersCache = new HashSet<string>();
			foreach (var county in this.Where(t => t.Rank == TitleRank.county)) {
				var holderId = county.GetHolderId(date);
				if (holderId != "0") {
					countyHoldersCache.Add(holderId);
				}
			}

			return countyHoldersCache;
		}

		public void ImportDevelopmentFromImperator(ProvinceCollection ck3Provinces, Date date, double irCivilizationWorth) {
			static (Dictionary<string, int>, Dictionary<ulong, int>) GetIRProvsPerCounty(ProvinceCollection ck3Provinces, IEnumerable<Title> counties) {
				var impProvsPerCounty = new Dictionary<string, int>();
				var ck3ProvsPerImperatorProv = new Dictionary<ulong, int>();
				foreach (var county in counties) {
					var imperatorProvs = new HashSet<ulong>();
					foreach (var ck3ProvId in county.CountyProvinces) {
						var ck3Province = ck3Provinces[ck3ProvId];
						var sourceProvinces = ck3Province.ImperatorProvinces;
						foreach (var irProvince in sourceProvinces) {
							imperatorProvs.Add(irProvince.Id);
							ck3ProvsPerImperatorProv.TryGetValue(irProvince.Id, out var currentValue);
							ck3ProvsPerImperatorProv[irProvince.Id] = currentValue + 1;
						}
					}

					impProvsPerCounty[county.Id] = imperatorProvs.Count;
				}

				return (impProvsPerCounty, ck3ProvsPerImperatorProv);
			}

			static bool IsCountyOutsideImperatorMap(Title county, IReadOnlyDictionary<string, int> impProvsPerCounty) {
				return impProvsPerCounty[county.Id] == 0;
			}

			double CalculateCountyDevelopment(Title county, IReadOnlyDictionary<ulong, int> ck3ProvsPerIRProv) {
				double dev = 0;
				var countyProvinces = county.CountyProvinces;
				var provsCount = 0;
				foreach (var ck3ProvId in countyProvinces) {
					++provsCount;
					var ck3Province = ck3Provinces[ck3ProvId];
					var sourceProvinces = ck3Province.ImperatorProvinces;
					if (sourceProvinces.Count == 0) {
						continue;
					}

					dev += sourceProvinces.Average(srcProv => srcProv.CivilizationValue / ck3ProvsPerIRProv[srcProv.Id]);
				}

				dev *= irCivilizationWorth;
				dev /= provsCount;
				dev -= Math.Sqrt(dev);
				return dev;
			}

			Logger.Info("Importing development from Imperator...");

			var counties = this.Where(t => t.Rank == TitleRank.county).ToList();
			var (irProvsPerCounty, ck3ProvsPerImperatorProv) = GetIRProvsPerCounty(ck3Provinces, counties);

			foreach (var county in counties) {
				if (IsCountyOutsideImperatorMap(county, irProvsPerCounty)) {
					// Don't change development for counties outside of Imperator map.
					continue;
				}

				double dev = CalculateCountyDevelopment(county, ck3ProvsPerImperatorProv);

				county.History.Fields.Remove("development_level");
				county.History.AddFieldValue(date, "development_level", "change_development_level", (int)dev);
			}

			Logger.IncrementProgress();
		}

		public IEnumerable<Title> GetCountriesImportedFromImperator() {
			return this.Where(t => t.ImperatorCountry is not null);
		}

		public Color GetDerivedColor(Color baseColor) {
			HashSet<Color> usedColors = this.Select(t => t.Color1).Where(c => c is not null && Math.Abs(c.H - baseColor.H) < 0.001).ToHashSet()!;

			for (double v = 0.05; v <= 1; v += 0.02) {
				var newColor = new Color(baseColor.H, baseColor.S, v);
				if (usedColors.Contains(newColor)) {
					continue;
				}
				return newColor;
			}

			Logger.Warn($"Couldn't generate new color from base {baseColor.OutputRgb()}");
			return baseColor;
		}

		private readonly HistoryFactory titleHistoryFactory = new HistoryFactory.HistoryFactoryBuilder()
			.WithSimpleField("holder", new OrderedSet<string> { "holder", "holder_ignore_head_of_faith_requirement" }, null)
			.WithSimpleField("government", "government", null)
			.WithSimpleField("liege", "liege", null)
			.WithSimpleField("development_level", "change_development_level", null)
			.WithSimpleField("succession_laws", "succession_laws", new SortedSet<string>())
			.Build();

		public void LoadHistory(Configuration config, ModFilesystem ck3ModFS) {
			var ck3BookmarkDate = config.CK3BookmarkDate;

			int loadedHistoriesCount = 0;

			var titlesHistoryParser = new Parser();
			titlesHistoryParser.RegisterRegex(Regexes.TitleId, (reader, titleName) => {
				var historyItem = reader.GetStringOfItem().ToString();
				if (!historyItem.Contains('{')) {
					return;
				}

				if (!TryGetValue(titleName, out var title)) {
					return;
				}

				var tempReader = new BufferedReader(historyItem);

				titleHistoryFactory.UpdateHistory(title.History, tempReader);
				++loadedHistoriesCount;
			});
			titlesHistoryParser.RegisterRegex(CommonRegexes.Catchall, ParserHelpers.IgnoreAndLogItem);

			Logger.Info("Parsing title history...");
			titlesHistoryParser.ParseGameFolder("history/titles", ck3ModFS, "txt", true);
			Logger.Info($"Loaded {loadedHistoriesCount} title histories.");

			// Add vanilla development to counties
			// For counties that inherit development level from de jure lieges, assign it to them directly for better reliability.
			foreach (var title in this.Where(t => t.Rank == TitleRank.county && t.GetDevelopmentLevel(ck3BookmarkDate) is null)) {
				var inheritedDev = title.GetOwnOrInheritedDevelopmentLevel(ck3BookmarkDate);
				title.SetDevelopmentLevel(inheritedDev ?? 0, ck3BookmarkDate);
			}

			// Remove history entries past the bookmark date.
			foreach (var title in this) {
				title.RemoveHistoryPastDate(ck3BookmarkDate);
			}
		}

		public void LoadCulturalNamesFromConfigurables() {
			const string filePath = "configurables/cultural_title_names.txt";
			Logger.Info($"Loading cultural title names from \"{filePath}\"...");

			var parser = new Parser();
			parser.RegisterRegex(CommonRegexes.String, (reader, titleId) => {
				var nameListToLocKeyDict = reader.GetAssignments();

				if (!TryGetValue(titleId, out var title)) {
					return;
				}
				if (title.CulturalNames is null) {
					title.CulturalNames = nameListToLocKeyDict;
				} else {
					foreach (var (nameList, locKey) in nameListToLocKeyDict) {
						title.CulturalNames[nameList] = locKey;
					}
				}
			});
			parser.IgnoreAndLogUnregisteredItems();
			parser.ParseFile(filePath);
		}
	}
}