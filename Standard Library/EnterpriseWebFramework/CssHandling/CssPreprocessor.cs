﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RedStapler.StandardLibrary.EnterpriseWebFramework.CssHandling {
	/// <summary>
	/// Allows custom elements, constants, and other feature extensions to CSS.
	/// </summary>
	internal class CssPreprocessor {
		private const string reservedCustomElementPrefix = "ewf";
		private const string customElementPattern = @"(?<!\.|#)" + reservedCustomElementPrefix + @"\w+";

		/// <summary>
		/// Processes the source text and returns the processed text. Use a different overload if you want the source or output to be a file.
		/// </summary>
		internal static string TransformCssFile( string sourceCssText ) {
			sourceCssText = RegularExpressions.RemoveMultiLineCStyleComments( sourceCssText );

			var customElementsDetected = from Match match in Regex.Matches( sourceCssText, customElementPattern ) select match.Value;
			customElementsDetected = customElementsDetected.Distinct();
			var knownCustomElements = CssHandlingStatics.Elements.Select( ce => reservedCustomElementPrefix + ce.Name );
			var unknownCustomElements = customElementsDetected.Except( knownCustomElements ).ToList();
			if( unknownCustomElements.Any() ) {
				throw new MultiMessageApplicationException(
					unknownCustomElements.Select( e => "\"" + e + "\" begins with the reserved custom element prefix but is not a known custom element." ).ToArray() );
			}

			using( var writer = new StringWriter() ) {
				using( var reader = new StringReader( sourceCssText ) ) {
					int cAsInt;
					var selectorBuffer = new StringBuilder();
					while( ( cAsInt = reader.Read() ) != -1 ) {
						var c = (char)cAsInt;
						if( selectorBuffer != null ) {
							if( c == ',' ) {
								writer.Write( getTransformedSelector( selectorBuffer.ToString() ) );
								writer.Write( c );
								selectorBuffer = new StringBuilder();
							}
							else if( c == '{' ) {
								writer.Write( getTransformedSelector( selectorBuffer.ToString() ) );
								writer.Write( c );
								selectorBuffer = null;
							}
							else
								selectorBuffer.Append( c );
						}
						else {
							writer.Write( c );
							if( c == '}' )
								selectorBuffer = new StringBuilder();
						}
					}
				}
				return writer.ToString();
			}
		}

		private static string getTransformedSelector( string selectorWithCustomElements ) {
			return StringTools.ConcatenateWithDelimiter( ",", getSelectors( selectorWithCustomElements ).ToArray() );
		}

		private static IEnumerable<string> getSelectors( string selectorWithCustomElements ) {
			var match = Regex.Match( selectorWithCustomElements, customElementPattern );
			if( !match.Success )
				yield return selectorWithCustomElements;
			else {
				foreach( var elementSelector in CssHandlingStatics.Elements.Single( i => reservedCustomElementPrefix + i.Name == match.Value ).Selectors ) {
					foreach( var selectorTail in getSelectors( selectorWithCustomElements.Substring( match.Index + match.Length ) ) )
						yield return selectorWithCustomElements.Substring( 0, match.Index ) + elementSelector + selectorTail;
				}
			}
		}
	}
}