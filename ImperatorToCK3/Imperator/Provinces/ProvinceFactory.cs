﻿using commonItems;
using System.Collections.Generic;
using System.Linq;

namespace ImperatorToCK3.Imperator.Provinces {
	public partial class Province {
		private static Province province = new(0);
		private static readonly Parser provinceParser = new();
		public static HashSet<string> IgnoredTokens { get; } = new();
		static Province() {
			provinceParser.RegisterKeyword("province_name", reader =>
				province.Name = new ProvinceName(reader).Name
			);
			provinceParser.RegisterKeyword("culture", reader =>
				province.Culture = ParserHelpers.GetString(reader)
			);
			provinceParser.RegisterKeyword("religion", reader =>
				province.Religion = ParserHelpers.GetString(reader)
			);
			provinceParser.RegisterKeyword("owner", reader =>
				province.OwnerCountry = new(ParserHelpers.GetULong(reader), null)
			);
			provinceParser.RegisterKeyword("controller", reader =>
				province.Controller = ParserHelpers.GetULong(reader)
			);
			provinceParser.RegisterKeyword("pop", reader =>
				province.parsedPopIds.Add(ParserHelpers.GetULong(reader))
			);
			provinceParser.RegisterKeyword("civilization_value", reader =>
				province.CivilizationValue = new SingleDouble(reader).Double
			);
			provinceParser.RegisterKeyword("province_rank", reader => {
				var provinceRankStr = ParserHelpers.GetString(reader);
				switch (provinceRankStr) {
					case "settlement":
						province.ProvinceRank = ProvinceRank.settlement;
						break;
					case "city":
						province.ProvinceRank = ProvinceRank.city;
						break;
					case "city_metropolis":
						province.ProvinceRank = ProvinceRank.city_metropolis;
						break;
					default:
						Logger.Warn($"Unknown province rank for province {province.Id}: {provinceRankStr}");
						break;
				}
			});
			provinceParser.RegisterKeyword("fort", reader =>
				province.Fort = new ParadoxBool(reader)
			);
			provinceParser.RegisterKeyword("holy_site", reader => {
				// 4294967295 is 2^32 − 1 and is the default value
				province.HolySite = ParserHelpers.GetULong(reader) != 4294967295;
			});
			provinceParser.RegisterKeyword("buildings", reader => {
				var buildingsList = ParserHelpers.GetInts(reader);
				province.BuildingCount = (uint)buildingsList.Sum();
			});
			provinceParser.RegisterRegex(CommonRegexes.Catchall, (reader, token) => {
				IgnoredTokens.Add(token);
				ParserHelpers.IgnoreItem(reader);
			});
		}
		public static Province Parse(BufferedReader reader, ulong provinceId) {
			province = new Province(provinceId);
			provinceParser.ParseStream(reader);
			return province;
		}
	}
}
