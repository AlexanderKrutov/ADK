﻿using Astrarium.Algorithms;
using Astrarium.Plugins.ASCOM.Controls;
using Astrarium.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Astrarium.Plugins.ASCOM
{
    public class Plugin : AbstractPlugin
    {
        private readonly IAscomProxy ascom;
        private readonly ISkyMap map;
        private readonly ISky sky;
        private readonly ISettings settings;

        public Plugin(ISkyMap map, ISky sky, ISettings settings)
        {
            var menuAscom = new MenuItem("$Menu.Telescope");

            this.ascom = Ascom.Proxy;
            this.map = map;
            this.sky = sky;
            this.settings = settings;

            SettingItems.Add(null, new SettingItem("ASCOMTelescopeId", ""));

            var menuConnectTelescope = new MenuItem("$Menu.ConnectToTelescope", new Command(ConnectTelescope));
            menuConnectTelescope.AddBinding(new SimpleBinding(this, nameof(IsConnectTelescopeVisible), nameof(MenuItem.IsVisible)));

            var menuDisconnectTelescope = new MenuItem("$Menu.DisconnectTelescope", new Command(DisconnectTelescope));
            menuDisconnectTelescope.AddBinding(new SimpleBinding(ascom, nameof(ascom.IsConnected), nameof(MenuItem.IsVisible)));
            menuDisconnectTelescope.AddBinding(new SimpleBinding(this, nameof(DisconnectTitle), nameof(MenuItem.Header)));

            var menuFindCurrentPoint = new MenuItem("$Menu.FindCurrentPoint", new Command(FindCurrentPoint));
            menuFindCurrentPoint.AddBinding(new SimpleBinding(ascom, nameof(ascom.IsConnected), nameof(MenuItem.IsEnabled)));

            var menuAbortSlew = new MenuItem("$Menu.AbortSlew", new Command(AbortSlew));
            menuAbortSlew.AddBinding(new SimpleBinding(ascom, nameof(ascom.IsSlewing), nameof(MenuItem.IsEnabled)));

            var menuFindHome = new MenuItem("$Menu.Home", new Command(FindHome));
            menuFindHome.AddBinding(new SimpleBinding(ascom, nameof(ascom.AtHome), nameof(MenuItem.IsChecked)));
            menuFindHome.AddBinding(new SimpleBinding(ascom, nameof(ascom.IsConnected), nameof(MenuItem.IsEnabled)));

            var menuPark = new MenuItem("$Menu.Park", new Command(ParkOrUnpark));
            menuPark.AddBinding(new SimpleBinding(ascom, nameof(ascom.AtPark), nameof(MenuItem.IsChecked)));
            menuPark.AddBinding(new SimpleBinding(ascom, nameof(ascom.IsConnected), nameof(MenuItem.IsEnabled)));

            var menuTrack = new MenuItem("$Menu.Track", new Command(SwitchTracking));
            menuTrack.AddBinding(new SimpleBinding(ascom, nameof(ascom.IsTracking), nameof(MenuItem.IsChecked)));
            menuTrack.AddBinding(new SimpleBinding(ascom, nameof(ascom.IsConnected), nameof(MenuItem.IsEnabled)));

            var menuInfo = new MenuItem("$Menu.AscomInformation", new Command(ShowInfo));
            menuInfo.AddBinding(new SimpleBinding(ascom, nameof(ascom.IsConnected), nameof(MenuItem.IsEnabled)));

            menuAscom.SubItems.Add(menuConnectTelescope);
            menuAscom.SubItems.Add(menuDisconnectTelescope);
            menuAscom.SubItems.Add(null);
            menuAscom.SubItems.Add(menuFindCurrentPoint);
            menuAscom.SubItems.Add(menuAbortSlew);
            menuAscom.SubItems.Add(null);
            menuAscom.SubItems.Add(menuFindHome);
            menuAscom.SubItems.Add(menuPark);
            menuAscom.SubItems.Add(menuTrack);
            menuAscom.SubItems.Add(null);
            menuAscom.SubItems.Add(menuInfo);

            MenuItems.Add(MenuItemPosition.MainMenuTop, menuAscom);

            var contextMenuAscom = new MenuItem("$ContextMenu.Telescope");
            contextMenuAscom.AddBinding(new SimpleBinding(this, nameof(IsContextMenuEnabled), nameof(MenuItem.IsEnabled)));

            var contextMenuSyncTo = new MenuItem("$ContextMenu.Telescope.Sync", new Command(SyncToPosition));
            contextMenuSyncTo.AddBinding(new SimpleBinding(this, nameof(IsContextMenuEnabled), nameof(MenuItem.IsEnabled)));

            var contextMenuSlewTo = new MenuItem("$ContextMenu.Telescope.Slew", new Command(SlewToPosition));
            contextMenuSlewTo.AddBinding(new SimpleBinding(this, nameof(IsContextMenuEnabled), nameof(MenuItem.IsEnabled)));

            contextMenuAscom.SubItems.Add(contextMenuSyncTo);
            contextMenuAscom.SubItems.Add(contextMenuSlewTo);

            MenuItems.Add(MenuItemPosition.ContextMenu, contextMenuAscom);

            ascom.PropertyChanged += Ascom_PropertyChanged;
            ascom.OnMessageShow += Ascom_OnMessageShow;

            SettingItems.Add("Colors", new SettingItem("TelescopeMarkerColor", new SkyColor(Color.DarkOrange)));
            SettingItems.Add("Fonts", new SettingItem("TelescopeMarkerFont", SystemFonts.DefaultFont));
            SettingItems.Add("Ascom", new SettingItem("TelescopeMarkerLabel", true));
            SettingItems.Add("Ascom", new SettingItem("TelescopeFindCurrentPointAfterConnect", false));
            SettingItems.Add("Ascom", new SettingItem("TelescopePollingPeriod", (decimal)500, typeof(UpDownSettingControl)));

            settings.SettingValueChanged += Settings_SettingValueChanged;
        }

        private void Settings_SettingValueChanged(string settingName, object value)
        {
            if (settingName == "TelescopePollingPeriod")
            {
                int period = (int)(decimal)value;
                if (period >= 100 && period <= 5000)
                {
                    ascom.PollingPeriod = period;
                }
            }
        }

        private void Ascom_OnMessageShow(string message)
        {
            ViewManager.ShowPopupMessage(message);
        }

        private void Ascom_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            NotifyPropertyChanged(
                nameof(IsShowSettingEnabled),
                nameof(IsConnectTelescopeVisible),
                nameof(DisconnectTitle),
                nameof(IsContextMenuEnabled)
            );
        }

        public bool IsConnectTelescopeVisible
        {
            get
            {
                return !ascom.IsAscomPlatformInstalled || (ascom.IsAscomPlatformInstalled && !ascom.IsConnected);
            }
        }

        public bool IsContextMenuEnabled
        {
            get
            {
                return ascom.IsAscomPlatformInstalled && ascom.IsConnected;
            }
        }

        public bool IsShowSettingEnabled
        {
            get
            {
                return ascom.IsAscomPlatformInstalled && !ascom.IsConnected;
            }
        }

        public string DisconnectTitle
        {
            get
            {
                return ascom.IsAscomPlatformInstalled && ascom.IsConnected ? $"{Text.Get("Menu.DisconnectTelescope")} {ascom.TelescopeName}" : Text.Get("Menu.DisconnectTelescope");
            }
        }

        private async void ConnectTelescope()
        {
            if (ascom.IsAscomPlatformInstalled)
            {
                string savedTelescopeId = settings.Get<string>("ASCOMTelescopeId");
                var telescopeId = ascom.Connect(savedTelescopeId);
                if (!string.IsNullOrEmpty(telescopeId))
                {
                    ascom.SetDateTime(DateTime.UtcNow);
                    ascom.SetLocation(settings.Get<CrdsGeographical>("ObserverLocation"));
                    if (!string.Equals(telescopeId, savedTelescopeId))
                    {
                        settings.Set("ASCOMTelescopeId", telescopeId);
                        settings.Save();
                    }
                    if (settings.Get("TelescopeFindCurrentPointAfterConnect"))
                    {
                        int period = (int)settings.Get<decimal>("TelescopePollingPeriod");
                        await Task.Delay(period);
                        FindCurrentPoint();
                    }
                }
            }
            else
            {
                ViewManager.ShowMessageBox("$NoAscom.Title", "$NoAscom.Message");
            }
        }

        private void DisconnectTelescope()
        {
            ascom.Disconnect();
        }

        private void FindCurrentPoint()
        {
            map.GoToPoint(ascom.Position.ToHorizontal(sky.Context.GeoLocation, sky.Context.SiderealTime), TimeSpan.FromSeconds(1));
        }

        private void AbortSlew()
        {
            ascom.AbortSlewing();
        }

        private void FindHome()
        {
            ascom.FindHome();
        }

        private void ParkOrUnpark()
        {
            if (ascom.AtPark)
            {
                ascom.Unpark();
            }
            else
            {
                ascom.Park();
            }
        }

        private void SwitchTracking()
        {
            ascom.SwitchTracking();
        }

        private void ShowInfo()
        {
            StringBuilder sb = new StringBuilder();

            var info = ascom.Info;

            sb.AppendLine($"**{Text.Get("TelescopeInfo.TelescopeName")}**  ");
            sb.AppendLine(info.TelescopeName);
            sb.AppendLine();

            sb.AppendLine($"**{Text.Get("TelescopeInfo.TelescopeDescription")}**  ");
            sb.AppendLine(info.TelescopeDescription);
            sb.AppendLine();

            sb.AppendLine($"**{Text.Get("TelescopeInfo.DriverVersion")}**  ");
            sb.AppendLine(info.DriverVersion);
            sb.AppendLine();

            sb.AppendLine($"**{Text.Get("TelescopeInfo.DriverDescription")}**  ");
            sb.AppendLine(info.DriverDescription);
            sb.AppendLine();

            sb.AppendLine($"**{Text.Get("TelescopeInfo.InterfaceVersion")}**  ");
            sb.AppendLine(info.InterfaceVersion);
            sb.AppendLine();

            sb.AppendLine($"**{Text.Get("TelescopeInfo.Capabilities")}**  ");
            sb.AppendLine($"CanFindHome: {info.CanFindHome}  ");
            sb.AppendLine($"CanSetTracking: {info.CanSetTracking}  ");
            sb.AppendLine($"CanSlew: {info.CanSlew}  ");
            sb.AppendLine($"CanSync: {info.CanSync}  ");
            sb.AppendLine($"CanPark: {info.CanPark}  ");
            sb.AppendLine($"CanUnpark: {info.CanUnpark}  ");

            ViewManager.ShowMessageBox("$TelescopeInfo.Title", sb.ToString());
        }

        private CrdsEquatorial GetMouseCoordinates()
        {
            var hor = map.SelectedObject?.Horizontal ?? map.MousePosition;
            var eq = hor.ToEquatorial(sky.Context.GeoLocation, sky.Context.SiderealTime);
            return eq;
        }

        private void SyncToPosition()
        {
            ascom.Sync(GetMouseCoordinates());
        }

        private void SlewToPosition()
        {
            ascom.Slew(GetMouseCoordinates());
        }
    }
}
