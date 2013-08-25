﻿using System;

namespace RedStapler.StandardLibrary {
	/// <summary>
	/// Extension methods for doubles.
	/// </summary>
	public static class DoubleTools {
		/// <summary>
		/// Rounds double value to nearest hundred integer.
		/// 'Nearest' defined by the passed MidpointRounding
		/// </summary>
		public static int RoundToHundred( this double val, MidpointRounding m ) {
			return (int)Math.Round( val / 100d, m ) * 100;
		}

		/// <summary>
		/// Rounds double value to nearest ten integer.
		/// 'Nearest' defined by the passed MidpointRounding
		/// </summary>
		public static int RoundToTen( this double val, MidpointRounding m ) {
			return (int)Math.Round( val / 10d, m ) * 10;
		}

		/// <summary>
		/// Returns the dollar amount to two decimal places prefixed with $. e.g. $8.99
		/// </summary>
		public static string ToMoneyString( this double d ) {
			return d.ToString( "c2" );
		}
	}
}