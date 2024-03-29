﻿using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using RedStapler.StandardLibrary.EnterpriseWebFramework.Controls;
using RedStapler.StandardLibrary.EnterpriseWebFramework.CssHandling;

namespace RedStapler.StandardLibrary.EnterpriseWebFramework {
	/// <summary>
	/// A WYSIWYG HTML editor.
	/// </summary>
	public class WysiwygHtmlEditor: WebControl, ControlTreeDataLoader, ControlWithJsInitLogic, FormControl {
		internal const string CkEditorFolderUrl = "Ewf/ThirdParty/CkEditor/ckeditor-4.1.2";

		private readonly FormValue<string> formValue;

		/// <summary>
		/// Creates a simple HTML editor. Do not pass null for value.
		/// </summary>
		public WysiwygHtmlEditor( string value ) {
			formValue = new FormValue<string>( () => value,
			                                   () => this.IsOnPage() ? UniqueID : "",
			                                   v => v,
			                                   rawValue => {
				                                   if( rawValue == null )
					                                   return PostBackValueValidationResult<string>.CreateInvalid();

				                                   // This hack prevents the NewLine that CKEditor seems to always add to the end of the textarea from causing
				                                   // ValueChangedOnPostBack to always return true.
				                                   if( rawValue.EndsWith( Environment.NewLine ) &&
				                                       rawValue.Remove( rawValue.Length - Environment.NewLine.Length ) == formValue.GetDurableValue() )
					                                   rawValue = formValue.GetDurableValue();

				                                   return PostBackValueValidationResult<string>.CreateValidWithValue( rawValue );
			                                   } );
		}

		void ControlTreeDataLoader.LoadData() {
			Attributes.Add( "name", UniqueID );
			PreRender += delegate { EwfTextBox.AddTextareaValue( this, formValue.GetValue( AppRequestState.Instance.EwfPageRequestState.PostBackValues ) ); };
		}

		string ControlWithJsInitLogic.GetJsInitStatements() {
			const string toolbar =
				"[ 'Source', '-', 'Bold', 'Italic', '-', 'NumberedList', 'BulletedList', '-', 'JustifyLeft', 'JustifyCenter', 'JustifyRight', 'JustifyBlock', '-', 'Image', 'Table', 'HorizontalRule', '-', 'Link', 'Unlink', 'Styles' ]";
			var contentsCss = this.GetClientUrl( "~/" + CkEditorFolderUrl + "/contents" + CssHandler.GetFileVersionString( DateTime.MinValue ) + ".css" );
			return "CKEDITOR.replace( '" + ClientID + "', { toolbar: [ " + toolbar + " ], contentsCss: '" + contentsCss + "' } );";
		}

		FormValue FormControl.FormValue { get { return formValue; } }

		/// <summary>
		/// Gets the post back value.
		/// </summary>
		public string GetPostBackValue( PostBackValueDictionary postBackValues ) {
			return formValue.GetValue( postBackValues );
		}

		/// <summary>
		/// Returns true if the value changed on this post back.
		/// </summary>
		public bool ValueChangedOnPostBack( PostBackValueDictionary postBackValues ) {
			return formValue.ValueChangedOnPostBack( postBackValues );
		}

		/// <summary>
		/// Returns the tag that represents this control in HTML.
		/// </summary>
		protected override HtmlTextWriterTag TagKey { get { return HtmlTextWriterTag.Textarea; } }
	}
}