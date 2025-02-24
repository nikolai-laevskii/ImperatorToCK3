﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImperatorToCK3.CK3.Characters;

public class DNA {
	public class PaletteCoordinates {
		// hair, skin and eye color palettes are 256x256
		public int X { get; init; } = 128;
		public int Y { get; init; } = 128;
	}

	public string Id { get; }
	
	private readonly Dictionary<string, DNAColorGeneValue> colorDNAValues;
	private readonly Dictionary<string, DNAGeneValue> morphDNAValues;
	private readonly Dictionary<string, DNAGeneValue> accessoryDNAValues;
	public IReadOnlyDictionary<string, DNAGeneValue> AccessoryDNAValues => accessoryDNAValues;

	public IEnumerable<string> DNALines {
		get {
			var colorLines = colorDNAValues
				.Select(kvp => $"{kvp.Key}={{ {kvp.Value} }}");
			var morphGeneLines = morphDNAValues
				.Select(kvp => $"{kvp.Key}={{ {kvp.Value} }}");
			var accessoryGeneLines = accessoryDNAValues
				.Select(kvp => $"{kvp.Key}={{ {kvp.Value} }}");
			return colorLines.Concat(morphGeneLines).Concat(accessoryGeneLines);
		}
	}

	public DNA(
		string id,
		IDictionary<string, DNAColorGeneValue> colorDNAValues,
		IDictionary<string, DNAGeneValue> morphDNAValues,
		IDictionary<string, DNAGeneValue> accessoryDNAValues
	) {
		Id = id;
		this.colorDNAValues = new Dictionary<string, DNAColorGeneValue>(colorDNAValues);
		this.morphDNAValues = new Dictionary<string, DNAGeneValue>(morphDNAValues);
		this.accessoryDNAValues = new Dictionary<string, DNAGeneValue>(accessoryDNAValues);
	}

	public void OutputGenes(StreamWriter output) {
		output.WriteLine("\t\tgenes={");

		foreach (var dnaLine in DNALines) {
			output.WriteLine($"\t\t\t{dnaLine}");
		}

		output.WriteLine("\t\t}");
	}
}