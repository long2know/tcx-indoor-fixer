using System;
using System.Collections.Generic;
using System.Text;
using tcx_util.Utilities.Interfaces;

namespace tcx_util.Utilities
{
	public class Coordinates
	{
		public double Latitude { get; set; }
		public double Longitude { get; set; }
	}
}

namespace tcx_util.Utilities.Interfaces
{
	public interface IGpsService
	{
		double CalculateDistance(Coordinates start, Coordinates end, bool convertToMeters = true);
		double CalculateDistance(IEnumerable<Coordinates> coordinates, bool convertToMeters = true);
	}
}

namespace tcx_util.Utilities.Services
{
	public class GpsService : IGpsService
	{
		public double CalculateDistance(Coordinates start, Coordinates end, bool convertToMeters = true)
		{
			var distance = 0.0D;
			var factor = convertToMeters ? 1000.0D : 1.0D;
			if (start.Latitude != 0.0D && start.Longitude != 0.0D && end.Latitude != 0.0D && end.Longitude != 0.0D)
			{
				var lat1 = start.Latitude.ToRadians();
				var lon1 = start.Longitude.ToRadians();
				var lat2 = end.Latitude.ToRadians();
				var lon2 = end.Longitude.ToRadians();

				double longdis = (start.Longitude - end.Longitude).ToRadians(); //calculating longitudinal difference
				double angudis = Math.Sin(lat1) * Math.Sin(lat2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(longdis);
				angudis = Math.Acos(angudis); //converted back to radians
				distance = angudis * 6372.795; //multiplied by the radius of the Earth
			}

			// Distance will be in KM, but convert to meters if desired
			return distance / factor;
		}

		public double CalculateDistance(IEnumerable<Coordinates> coordinates, bool convertToMeters = true)
		{
			var enumerator = coordinates.GetEnumerator();
			enumerator.MoveNext();
			var current = enumerator.Current;
			var distance = 0.0D;

			while (enumerator.MoveNext())
			{
				var next = enumerator.Current;
				distance += CalculateDistance(current, next, convertToMeters);
				current = next;
			}

			return distance;
		}
	}
}

public static class NumericExtensions
{
	public static double ToRadians(this double val)
	{
		return (Math.PI / 180.0) * val;
	}
}
