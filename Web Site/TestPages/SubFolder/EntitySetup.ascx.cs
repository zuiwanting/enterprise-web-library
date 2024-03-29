using System.Collections.Generic;
using System.Web.UI;
using RedStapler.StandardLibrary.EnterpriseWebFramework;
using RedStapler.StandardLibrary.EnterpriseWebFramework.DisplayElements.Entity;
using RedStapler.StandardLibrary.EnterpriseWebFramework.Ui;
using RedStapler.StandardLibrary.EnterpriseWebFramework.Ui.Entity;

namespace EnterpriseWebLibrary.WebSite.TestPages.SubFolder {
	public partial class EntitySetup: UserControl, EntityDisplaySetup {
		public partial class Info: TabModeOverrider {
			protected override PageInfo createParentPageInfo() {
				return ActionControls.GetInfo();
			}

			protected override List<PageGroup> createPageInfos() {
				return new List<PageGroup> { new PageGroup( new General.Info( this ), new Details.Info( this ), new Disabled.Info( this ), new New.Info( this ) ) };
			}

			public override string EntitySetupName { get { return ""; } }

			TabMode TabModeOverrider.GetTabMode() {
				return TabMode.Horizontal;
			}
		}

		void EntitySetupBase.LoadData() {}

		public List<ActionButtonSetup> CreateNavButtonSetups() {
			return new List<ActionButtonSetup>();
		}

		public List<LookupBoxSetup> CreateLookupBoxSetups() {
			return new List<LookupBoxSetup>();
		}

		public List<ActionButtonSetup> CreateActionButtonSetups() {
			return new List<ActionButtonSetup>();
		}
	}
}