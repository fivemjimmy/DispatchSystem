﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;

using MaterialSkin;
using MaterialSkin.Controls;

using DispatchSystem.Common.DataHolders;
using DispatchSystem.Common.DataHolders.Storage;
using DispatchSystem.Common.NetCode;

namespace DispatchSystem.cl.Windows
{
    public partial class MultiOfficerView : MaterialForm, ISyncable
    {
        StorageManager<Officer> data;

        public MultiOfficerView(StorageManager<Officer> input)
        {
            this.Icon = Icon.ExtractAssociatedIcon("icon.ico");
            InitializeComponent();

            data = input;
            UpdateCurrentInformation();
        }

        public bool IsCurrentlySyncing { get; private set; }
        public DateTime LastSyncTime { get; private set; } = DateTime.Now;

        public void UpdateCurrentInformation()
        {
            List<ListViewItem> lvis = new List<ListViewItem>();

            officers.Items.Clear();

            for (int i = 0; i < data.Count; i++)
            {
                ListViewItem lvi = new ListViewItem(data[i].PlayerName);
                lvi.SubItems.Add(data[i].Status == OfficerStatus.OffDuty ? "Off Duty" : data[i].Status == OfficerStatus.OnDuty ? "On Duty" : "Busy");
                lvis.Add(lvi);
            }

            officers.Items.AddRange(lvis.ToArray());
        }

        public async Task Resync(bool skipTime)
        {
            if (((DateTime.Now - LastSyncTime).Seconds < 15 || IsCurrentlySyncing) && !skipTime)
            {
                MessageBox.Show($"You must wait 15 seconds before the last sync time \nSeconds to wait: {15 - (DateTime.Now - LastSyncTime).Seconds}", "DispatchSystem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LastSyncTime = DateTime.Now;
            IsCurrentlySyncing = true;

            Socket usrSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try { usrSocket.Connect(Config.IP, Config.Port); }
            catch (SocketException) { MessageBox.Show("Connection Refused or failed!\nPlease contact the owner of your server", "DispatchSystem", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            NetRequestHandler handle = new NetRequestHandler(usrSocket);

            Tuple<NetRequestResult, StorageManager<Officer>> result = await handle.TryTriggerNetFunction<StorageManager<Officer>>("GetOfficers");
            usrSocket.Shutdown(SocketShutdown.Both);
            usrSocket.Close();

            if (result.Item2 != null)
            {
                Invoke((MethodInvoker)delegate
                {
                    data = result.Item2;
                    UpdateCurrentInformation();
                });
            }
            else
                MessageBox.Show("FATAL: Invalid", "DispatchSystem", MessageBoxButtons.OK, MessageBoxIcon.Error);

            IsCurrentlySyncing = false;
        }

        private async void OnResyncClick(object sender, EventArgs e) =>
#if DEBUG
            await Resync(true);
#else
            await Resync(false);
#endif

        private void OnMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (officers.FocusedItem.Bounds.Contains(e.Location))
                {
                    rightClickMenu.Show(Cursor.Position);
                }
            }
        }
        private async void OnSelectStatusClick(object sender, EventArgs e)
        {
            ListViewItem focusesItem = officers.SelectedItems[0];
            int index = officers.Items.IndexOf(focusesItem);
            Officer ofc = data[index];

            Socket usrSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try { usrSocket.Connect(Config.IP, Config.Port); }
            catch (SocketException) { MessageBox.Show("Connection Refused or failed!\nPlease contact the owner of your server", "DispatchSystem", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            NetRequestHandler handle = new NetRequestHandler(usrSocket);

            do
            {
                if (sender == (object)statusOnDutyStripItem)
                {
                    if (ofc.Status == OfficerStatus.OnDuty)
                    {
                        MessageBox.Show("Really? That officer is already on duty!", "DispatchSystem", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                        break;
                    }

                    await handle.TryTriggerNetEvent("SetStatus", ofc, OfficerStatus.OnDuty);
                }
                else if (sender == (object)statusOffDutyStripItem)
                {
                    if (ofc.Status == OfficerStatus.OffDuty)
                    {
                        MessageBox.Show("Really? That officer is already off duty!", "DispatchSystem", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                        break;
                    }

                    await handle.TryTriggerNetEvent("SetStatus", ofc, OfficerStatus.OffDuty);
                }
                else
                {
                    if (ofc.Status == OfficerStatus.Busy)
                    {
                        MessageBox.Show("Really? That officer is already busy!", "DispatchSystem", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                        break;
                    }

                    await handle.TryTriggerNetEvent("SetStatus", ofc, OfficerStatus.Busy);
                }
            } while (false);

            usrSocket.Shutdown(SocketShutdown.Both);
            usrSocket.Close();
            await Resync(true); 
        }
    }
}
