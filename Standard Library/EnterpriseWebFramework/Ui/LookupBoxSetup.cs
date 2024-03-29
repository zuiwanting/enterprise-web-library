using System;
using System.Web.UI.WebControls;
using RedStapler.StandardLibrary.EnterpriseWebFramework.Controls;

namespace RedStapler.StandardLibrary.EnterpriseWebFramework.Ui {
	/// <summary>
	/// Represents a textbox that appears in the navigation bar allowing users to lookup a record by number.
	/// </summary>
	public class LookupBoxSetup {
		private readonly int pixelWidth;
		private readonly string defaultText;
		private readonly PageInfo autoCompleteService;
		private readonly string postBackId;
		private readonly Func<string, PageInfo> handler;

		/// <summary>
		/// Creates a lookup box.
		/// </summary>
		/// <param name="pixelWidth"></param>
		/// <param name="defaultText">Text displayed when the LookupBox does not have focus.</param>
		/// <param name="postBackId"></param>
		/// <param name="handler">Supplies the string entered into the LookupBox from the user. Returns the page the user will be redirected to.</param>
		/// <param name="autoCompleteService"></param>
		public LookupBoxSetup( int pixelWidth, string defaultText, string postBackId, Func<string, PageInfo> handler, PageInfo autoCompleteService = null ) {
			this.pixelWidth = pixelWidth;
			this.defaultText = defaultText;
			this.autoCompleteService = autoCompleteService;
			this.postBackId = postBackId;
			this.handler = handler;
		}

		/// <summary>
		/// Builds this LookupBox and returns the panel.
		/// </summary>
		public WebControl BuildLookupBoxPanel() {
			var val = new DataValue<string>();
			var postBack = PostBack.CreateFull( id: postBackId, actionGetter: () => new PostBackAction( handler( val.Value ) ) );

			var textBox = FormItem.Create( "",
			                               new EwfTextBox( "", postBack: postBack ) { Width = new Unit( pixelWidth ) },
			                               validationGetter: control => new Validation( ( pbv, validator ) => val.Value = control.GetPostBackValue( pbv ), postBack ) );
			textBox.Control.SetWatermarkText( defaultText );
			if( autoCompleteService != null )
				textBox.Control.SetupAutoComplete( autoCompleteService, AutoCompleteOption.PostBackOnItemSelect );

			return new Block( textBox.ToControl() ) { CssClass = "ewfLookupBox" };
		}
	}
}