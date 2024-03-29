﻿using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using RedStapler.StandardLibrary.EnterpriseWebFramework.Controls;
using RedStapler.StandardLibrary.IO;
using RedStapler.StandardLibrary.WebFileSending;

namespace RedStapler.StandardLibrary.EnterpriseWebFramework {
	/// <summary>
	/// Useful methods that require a web context.
	/// </summary>
	public static class EwfExtensionMethods {
		/// <summary>
		/// Saves the given Excel work book to the response stream. It is not necessary to save the workbook before calling this method.
		/// Automatically executes inside an EhModifyDataAndSendFile method. Automatically converts the given file name to a safe file name.
		/// </summary>
		public static void SendExcelFile( this ExcelFileWriter workbook, string fileNameWithoutExtension ) {
			EwfPage.Instance.EhModifyDataAndSendFile( new FileCreator( stream => {
				workbook.SaveToStream( stream );
				return new FileInfoToBeSent( workbook.GetSafeFileName( fileNameWithoutExtension ), workbook.ContentType );
			} ) );
		}

		/// <summary>
		/// Returns a System.Web.UI.WebControls.Literal that contains an HTML encoded version of this string.
		/// // NOTE: This should be renamed to "ToControl."
		/// </summary>
		public static Literal GetLiteralControl( this string s, bool returnNonBreakingSpaceIfEmpty = true ) {
			return new Literal { Text = s.GetTextAsEncodedHtml( returnNonBreakingSpaceIfEmpty: returnNonBreakingSpaceIfEmpty ) };
		}

		/// <summary>
		/// Returns an EWF label control with the given text.
		/// EWF Labels automatically HTML encode text.
		/// </summary>
		public static EwfLabel GetLabelControl( this string s ) {
			return new EwfLabel { Text = s };
		}

		internal static bool ShouldBeSecureGivenCurrentRequest( this ConnectionSecurity connectionSecurity, bool isIntermediateInstallationPublicPage ) {
			// Intermediate installations must be secure because the intermediate user cookie is secure.
			if( AppTools.IsIntermediateInstallation && !isIntermediateInstallationPublicPage )
				return true;

			return connectionSecurity == ConnectionSecurity.MatchingCurrentRequest
				       ? HttpContext.Current.Request.IsSecureConnection
				       : connectionSecurity == ConnectionSecurity.SecureIfPossible && EwfApp.SupportsSecureConnections;
		}

		/// <summary>
		/// Adds the given controls to this control and returns this control.
		/// Equivalent to thisControl.Controls.Add(otherControl) in a foreach loop.
		/// </summary>
		/// <param name="control"></param>
		/// <param name="controls">Controls to add to this control</param>
		/// <returns>This calling control</returns>
		public static T AddControlsReturnThis<T>( this T control, params Control[] controls ) where T: Control {
			return control.AddControlsReturnThis( controls as IEnumerable<Control> );
		}

		/// <summary>
		/// Adds the given controls to this control and returns this control.
		/// Equivalent to thisControl.Controls.Add(otherControl) in a foreach loop.
		/// </summary>
		/// <param name="control"></param>
		/// <param name="controls">Controls to add to this control</param>
		/// <returns>This calling control</returns>
		public static T AddControlsReturnThis<T>( this T control, IEnumerable<Control> controls ) where T: Control {
			foreach( var c in controls )
				control.Controls.Add( c );
			return control;
		}

		/// <summary>
		/// Converts a string to an EwfTableCell.
		/// </summary>
		public static EwfTableCell ToCell( this string text ) {
			return new EwfTableCell( text );
		}

		/// <summary>
		/// Converts a control to an EwfTableCell.
		/// </summary>
		public static EwfTableCell ToCell( this Control control ) {
			return new EwfTableCell( control );
		}
	}
}