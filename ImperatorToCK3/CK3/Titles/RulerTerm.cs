﻿using commonItems;
using commonItems.Localization;
using ImperatorToCK3.CK3.Characters;
using ImperatorToCK3.Mappers.Culture;
using ImperatorToCK3.Mappers.Government;
using ImperatorToCK3.Mappers.Nickname;
using ImperatorToCK3.Mappers.Province;
using ImperatorToCK3.Mappers.Religion;

namespace ImperatorToCK3.CK3.Titles;

public class RulerTerm {
	public string CharacterId { get; } = "0";
	public Date StartDate { get; }
	public string? Government { get; }
	public Imperator.Countries.RulerTerm.PreImperatorRulerInfo? PreImperatorRuler { get; }

	public RulerTerm(
		Imperator.Countries.RulerTerm imperatorRulerTerm,
		Characters.CharacterCollection characters,
		GovernmentMapper governmentMapper,
		LocDB locDB,
		ReligionMapper religionMapper,
		CultureMapper cultureMapper,
		NicknameMapper nicknameMapper,
		ProvinceMapper provinceMapper,
		Configuration config
	) {
		if (imperatorRulerTerm.CharacterId is not null) {
			CharacterId = $"imperator{imperatorRulerTerm.CharacterId}";
		}
		StartDate = imperatorRulerTerm.StartDate;
		if (imperatorRulerTerm.Government is not null) {
			Government = governmentMapper.GetCK3GovernmentForImperatorGovernment(imperatorRulerTerm.Government, null);
		}

		PreImperatorRuler = imperatorRulerTerm.PreImperatorRuler;
		if (PreImperatorRuler?.BirthDate is null) {
			return;
		}
		if (PreImperatorRuler.DeathDate is null) {
			return;
		}
		if (PreImperatorRuler.Country is not null) {
			// create a new ruler character
			var character = new Character(
				PreImperatorRuler,
				StartDate,
				PreImperatorRuler.Country,
				characters,
				locDB,
				religionMapper,
				cultureMapper,
				nicknameMapper,
				provinceMapper,
				config
			);
			characters.Add(character);
			CharacterId = character.Id;
		}
	}
}