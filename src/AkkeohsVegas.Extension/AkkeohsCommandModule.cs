using System;
using System.Collections;
using ScriptPortal.Vegas;
using AkkeohsVegas.Core;

namespace AkkeohsVegas.Extension
{

    public class AkkeohsCommandModule : ICustomCommandModule
    {
        public const string CommandName = "AkkeohSubtitler";
        public const string WindowTitle = ProductInfo.DisplayName;

        private Vegas _vegas;
        private readonly CustomCommand _command =
            new CustomCommand(CommandCategory.View, CommandName);

        public void InitializeModule(Vegas vegas)
        {
            _vegas = vegas;
            _command.DisplayName = WindowTitle;
        }

        public ICollection GetCustomCommands()
        {
            _command.MenuPopup += HandleMenuPopup;
            _command.Invoked += HandleInvoked;
            return new[] { _command };
        }

        private void HandleMenuPopup(object sender, EventArgs e)
        {
            _command.Checked = _vegas != null && _vegas.FindDockView(AkkeohsDockView.ViewId);
        }

        private void HandleInvoked(object sender, EventArgs e)
        {
            if (_vegas == null)
                return;

            try
            {
                if (_vegas.ActivateDockView(AkkeohsDockView.ViewId))
                    return;

                AkkeohsDockView dockView = new AkkeohsDockView(_vegas);
                _vegas.LoadDockView(dockView);
            }
            catch (Exception ex)
            {
                string detail = ex.Message;
                if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                    detail += Environment.NewLine + ex.InnerException.Message;

                System.Windows.Forms.MessageBox.Show(
                    "Could not open the panel." + Environment.NewLine + Environment.NewLine + detail,
                    ProductInfo.DisplayName,
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}
