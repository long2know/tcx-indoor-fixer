using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace tcx_util
{
	public class Application
	{
		public void Run(string[] args)
		{
			var distance = 0.0m;
			var totalTime = 0.0m;
			var inFileName = string.Empty;
			var outFileName = string.Empty;

			var helpRequested = (new List<int> { Array.IndexOf(args, "--h"), Array.IndexOf(args, "--help"), Array.IndexOf(args, @"/h"), Array.IndexOf(args, @"/help") }).Max();
			var distanceIndexOf = (new List<int> { Array.IndexOf(args, "--d"), Array.IndexOf(args, "--distance") }).Max();
			var totalTimeIndexOf = (new List<int> { Array.IndexOf(args, "--t"), Array.IndexOf(args, "--time") }).Max();
			var inFileNameIndexOf = (new List<int> { Array.IndexOf(args, "--i"), Array.IndexOf(args, "--input") }).Max();
			var outFileNameIndexOf = (new List<int> { Array.IndexOf(args, "--o"), Array.IndexOf(args, "--output") }).Max();

			if (helpRequested > -1 || inFileNameIndexOf == -1)
			{
				Console.WriteLine("Pass --d or --distance with specified distance in meters.");
				Console.WriteLine("Pass --t or --time to specified totla time in seconds.");
				Console.WriteLine("Pass -i or --input with the full path of the file to input.");
				Console.WriteLine("Pass -o or --output with the full path of the file to output.");
			}

			if (totalTimeIndexOf > -1 && args.Length > totalTimeIndexOf)
			{
				var couldParse = Decimal.TryParse(args[totalTimeIndexOf + 1], out totalTime);
				if (couldParse)
				{
					Console.WriteLine($"You passed in {totalTime} for total seconds");
				}
				else { totalTime = 0.0m; }
			}

			if (distanceIndexOf > -1 && args.Length > distanceIndexOf)
			{
				var couldParse = Decimal.TryParse(args[distanceIndexOf + 1], out distance);
				if (couldParse)
				{
					Console.WriteLine($"You passed in {distance} for total distance in meters.");
				}
				else { distance = 0.0m; }
			}

			if (inFileNameIndexOf > -1 && args.Length > inFileNameIndexOf)
			{
				inFileName = args[inFileNameIndexOf + 1];

				Console.WriteLine($"You passed in {inFileName} for input file.");
			}

			if (outFileNameIndexOf > -1 && args.Length > outFileNameIndexOf)
			{
				outFileName = args[outFileNameIndexOf + 1];
			}

			if (string.IsNullOrWhiteSpace(inFileName))
			{
				Console.WriteLine("No input file specified.");
			}

			var fileStream = new FileStream(inFileName, FileMode.Open);
			var reader = new StreamReader(fileStream);
			var xdoc = XDocument.Load(reader);

			XNamespace ns = xdoc.Root.GetDefaultNamespace();

			// Figure out if the XML is TCX or GPX
			if (ns.NamespaceName.Contains("http://www.garmin.com/xmlschemas/TrainingCenterDatabase"))
			{
				CreateTcxFromTcx(xdoc);
			}
			else if (ns.NamespaceName.Contains("http://www.topografix.com/GPX"))
			{
				CreateTcxFromGpx(xdoc);
			}
			else
			{
				throw new Exception("File is not TCX or GPX format");
			}
		}

		void CreateTcxFromTcx(XDocument xdoc, decimal distance = 0.0m, decimal totalTime = 0.0m)
		{
			XNamespace ns = xdoc.Root.GetDefaultNamespace();
			XNamespace extNs = "http://www.garmin.com/xmlschemas/ActivityExtension/v2";

			// Do we want to remove the extNs from the root?  It really depends on the parser that the
			// uploaded file uses
			xdoc.Descendants().Attributes().Where(a => a.IsNamespaceDeclaration || a.Value.Contains("http://www.garmin.com/xmlschemas/ActivityExtension/v2")).Remove();

			// Remove all
			var positions = xdoc.Descendants(ns + "Position").ToList();
			var altitudes = xdoc.Descendants(ns + "AltitudeMeters").ToList();

			foreach (var node in positions)
			{
				node.Remove();
			}

			foreach (var node in altitudes)
			{
				node.Remove();
			}

			// Now, get a count of the Trackpoints
			var trackPoints = xdoc.Descendants(ns + "Trackpoint").ToList();

			if (distance <= 0)
			{
				decimal.TryParse(xdoc.Descendants(ns + "DistanceMeters")?.FirstOrDefault()?.Value, out distance);
			}

			var count = trackPoints.Count();
			count = count <= 0 ? 1 : count;

			var averageDistance = distance / count;
			var averageSpeed = distance / (totalTime <= 0 ? 1.0m : totalTime);

			// We can also get the average time between points in the event that we want to correct the time(s)
			var firstTimeStamp = DateTime.Parse(trackPoints.First()?.Descendants(ns + "Time").FirstOrDefault()?.Value ?? DateTime.UtcNow.ToString());
			var lastTimeStamp = DateTime.Parse(trackPoints.Last()?.Descendants(ns + "Time").FirstOrDefault()?.Value ?? DateTime.UtcNow.ToString());

			decimal.TryParse(xdoc.Descendants(ns + "TotalTimeSeconds")?.FirstOrDefault()?.Value, out totalTime);

			if (totalTime <= 0)
			{
				var totalSeconds = (lastTimeStamp - firstTimeStamp).TotalSeconds; // Note that total seconds is also parsed above.
			}

			var timeBetweenPoints = totalTime / count;

			for (var i = 0; i < trackPoints.Count(); i++)
			{
				var time = trackPoints[i].Descendants(ns + "Time").FirstOrDefault();
				var timeStamp = firstTimeStamp.AddSeconds((double)(i * timeBetweenPoints));
				time.Value = timeStamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
			}

			// Replace the MaximumSpeed with the average speed
			var maxSpeed = xdoc.Descendants(ns + "MaximumSpeed")?.FirstOrDefault();
			if (maxSpeed != null)
			{
				maxSpeed.Value = $"{averageSpeed:N6}";
			}

			// Now replace all of the distances with the average.  Round to (3) decimal places
			var distanceAccumulator = 0.0m;

			foreach (var node in trackPoints)
			{
				distanceAccumulator += averageDistance;

				// Remove any DistanceMeters nodes
				node.Descendants(ns + "DistanceMeters")?.Remove();
				node.Descendants(ns + "Extensions").Remove();

				// Add a DistanceMeters node
				node.Add(new XElement(ns + "DistanceMeters", $"{distanceAccumulator:N3}"));

				node.Add(new XElement(ns + "Extensions",
					new XElement(extNs + "TPX",
						new XElement(extNs + "Speed", $"{averageSpeed:N6}")
						)
					));
			}

			// Now write the output
			var outStream = new FileStream("test-out.tcx", FileMode.Create);
			var outWrite = new System.IO.StreamWriter(outStream);
			xdoc.Save(outWrite);
		}

		void CreateTcxFromGpx(XDocument xdoc)
		{
			XNamespace xmlns = XNamespace.Get("http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2");
			XNamespace xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");
			XNamespace schemaLocation = XNamespace.Get("http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2 http://www.garmin.com/xmlschemas/TrainingCenterDatabasev2.xsd");

			XNamespace gpxns = xdoc.Root.GetDefaultNamespace();
			XNamespace gpxtpx = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

			// Get the metadata
			var metadata = xdoc.Descendants(gpxns + "metadata")?.FirstOrDefault() ?? new XElement("metadata");
			var trk = xdoc.Descendants(gpxns + "trk")?.FirstOrDefault() ?? new XElement("trk");
			var trkseg = trk.Descendants(gpxns + "trkseg")?.FirstOrDefault() ?? new XElement("trkseg");

			var firstpt = trkseg.Descendants(gpxns + "trkpt").First();
			var lastpt = trkseg.Descendants(gpxns + "trkpt").Last();

			DateTime.TryParse(firstpt.Descendants(gpxns + "time").First().Value, out DateTime startTime);
			DateTime.TryParse(lastpt.Descendants(gpxns + "time").First().Value, out DateTime endTime);
			var totalSeconds = (endTime - startTime).TotalSeconds;

			// Was there a distance, in meters, passed in?
			var distance = 5000;
			var count = trkseg.Descendants(gpxns + "trkpt").Count();
			count = count <= 0 ? 1 : count;
			var averageDistance = distance / count;

			var averageHR = trkseg.Descendants(gpxns + "trkpt").Select(x => decimal.Parse(x.Descendants(gpxtpx + "hr")?.FirstOrDefault()?.Value ?? "0.0")).Average();
			var maxHR = trkseg.Descendants(gpxns + "trkpt").Select(x => decimal.Parse(x.Descendants(gpxtpx + "hr")?.FirstOrDefault()?.Value ?? "0.0")).Max();
			var minHR = trkseg.Descendants(gpxns + "trkpt").Select(x => decimal.Parse(x.Descendants(gpxtpx + "hr")?.FirstOrDefault()?.Value ?? "0.0")).Min();

			var tcxDoc = new XDocument(
				new XElement(xmlns + "TrainingCenterDatabase",
					new XAttribute(XNamespace.Xmlns + "xsi", xsi),
					new XAttribute(xsi + "schemaLocation", schemaLocation)
				)
			);

			var ns = tcxDoc.Root.GetDefaultNamespace();

			tcxDoc.Root.Add(new XElement(ns + "Activities",
					new XElement(ns + "Activity",
						new XAttribute(ns + "Sport", "Running"),
						new XElement(ns + "Notes", $"<![CDATA[{trk.Descendants(gpxns + "name")?.FirstOrDefault()?.Value ?? "Run"}]]>"),
						new XElement(ns + "Id", metadata.Descendants(gpxns + "time")?.FirstOrDefault()?.Value ?? DateTime.UtcNow.ToString()),
						new XElement(ns + "Lap",
							new XElement(ns + "TotalTimeSeconds", totalSeconds),
							new XElement(ns + "DistanceMeters", distance),
							new XElement(ns + "AverageHeartRateBpm",
								new XElement(ns + "Value", averageHR)),
							new XElement(ns + "MaximumHeartRateBpm",
								new XElement(ns + "Value", maxHR)),
							new XElement(ns + "Intensity", "Active"),
							new XElement(ns + "TriggerMethod", "Manual"),
							new XElement(ns + "Track",
								trkseg.Descendants(gpxns + "trkpt").Select(x =>
									new XElement(ns + "Trackpoint",
										new XElement(ns + "Time", x.Descendants(gpxns + "time").FirstOrDefault()?.Value ?? DateTime.UtcNow.ToString()),
										new XElement(ns + "DistanceMeters", averageDistance),
										new XElement(ns + "HeartRateBpm",
											new XElement(ns + "Value", x.Descendants(gpxtpx + "hr")?.FirstOrDefault()?.Value ?? "0.0")
										)
									)
								)
							)
						)
					)
				));

			// Use our other routine to clean up the output, and to avoid duplication of logic.
			CreateTcxFromTcx(tcxDoc);
		}
	}
}

