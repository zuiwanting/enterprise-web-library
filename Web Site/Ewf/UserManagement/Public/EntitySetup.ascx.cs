using System.Collections.Generic;
using System.Web.UI;
using RedStapler.StandardLibrary.EnterpriseWebFramework.Controls;
using RedStapler.StandardLibrary.EnterpriseWebFramework.DisplayElements.Entity;
using RedStapler.StandardLibrary.EnterpriseWebFramework.Ui;

// Parameter: string destinationUrl

namespace RedStapler.StandardLibrary.EnterpriseWebFramework.EnterpriseWebLibrary.WebSite.UserManagement.Public {
	public partial class EntitySetup: UserControl, EntityDisplaySetup {
		partial class Info {
			protected override PageInfo createParentPageInfo() {
				return null;
			}

			protected override List<PageGroup> createPageInfos() {
				return new List<PageGroup>();
			}

			public override string EntitySetupName { get { return "Confirm Password Reset"; } }
		}

		void EntitySetupBase.LoadData() {}

		public List<ActionButtonSetup> CreateNavButtonSetups() {
			return new List<ActionButtonSetup> { new ActionButtonSetup( "Back", new EwfLink( new ExternalPageInfo( info.DestinationUrl ) ) ) };
		}

		public List<LookupBoxSetup> CreateLookupBoxSetups() {
			return new List<LookupBoxSetup>();
		}

		public List<ActionButtonSetup> CreateActionButtonSetups() {
			return new List<ActionButtonSetup>();
		}
	}
}