﻿using SpiceSharpRunner.Windows.Common;
using SpiceSharpRunner.Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SpiceSharp.Runner.Windows.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<IContent> Items { get; private set; }
        public Command NewNetlistCommand { get; }
        public Command OpenNetlistCommand { get; }
        public Command SaveNetlistCommand { get; }
        public Command RunSimulationCommand { get; }

        private IContent _selectedWindow = null;
        public IContent SelectedWindow
        {
            get
            {
                return this._selectedWindow;
            }
            set
            {
                this._selectedWindow = value;
                this.RaisePropertyChanged("SelectedWindow");
            }
        }

        public Dispatcher Dispatcher { get; }

        public MainWindowViewModel(Dispatcher dispatcher)
        {
            this.Items = new ObservableCollection<IContent>();
            this.NewNetlistCommand = new Command(NewNetlist, (p) => true);
            this.OpenNetlistCommand = new Command(OpenNetlist, (p) => true);
            this.RunSimulationCommand = new Command(RunSimulation, (p) => SelectedWindow is NetlistWindowViewModel);
            this.SaveNetlistCommand = new Command(SaveNetlist, (p) => SelectedWindow is NetlistWindowViewModel);
            Dispatcher = dispatcher;
        }

        private void OpenNetlist(object parameter)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Circuit files (*.cir)|*.cir|Netlist files (*.net)|*.net|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                var content = File.ReadAllText(openFileDialog.FileName);

                var item = new NetlistWindowViewModel(openFileDialog.FileName);
                item.Netlist = content;

                item.Closing += (s, e) => this.Items.Remove(item);
                this.Items.Add(item);
            }
        }

        private void SaveNetlist(object parameter)
        {
            if (SelectedWindow is NetlistWindowViewModel n)
            {
                if (n.Path == null)
                {
                    Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
                    saveFileDialog.Filter = "Circuit files (*.cir)|*.cir|Netlist files (*.net)|*.net|All files (*.*)|*.*";

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        File.WriteAllText(saveFileDialog.FileName, n.Netlist);
                        System.Windows.MessageBox.Show("Saved");
                        n.Path = saveFileDialog.FileName;
                        n.Dirty = false;
                    }
                }
                else
                {
                    File.WriteAllText(n.Path, n.Netlist);
                    n.Dirty = false;
                    System.Windows.MessageBox.Show("Saved");
                }
            }
        }

        private void RunSimulation(object parameter)
        {
            if (SelectedWindow is NetlistWindowViewModel n)
            {
                NetlistResultWindowViewModel netlistWindow = new NetlistResultWindowViewModel(Dispatcher);
                netlistWindow.Title = "Results for: " + n.Title;
                netlistWindow.Netlist = n.Netlist;
                netlistWindow.Run();
                this.Items.Add(netlistWindow);
            }
        }

        private void NewNetlist(object parameter)
        {
            var item = new NetlistWindowViewModel(null);
            item.Closing += (s, e) => this.Items.Remove(item);
            this.Items.Add(item);
        }
    }
}