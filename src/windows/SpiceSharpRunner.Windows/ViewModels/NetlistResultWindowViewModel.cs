﻿using SpiceSharp.Components;
using SpiceSharp.Runner.Windows.ViewModels;
using SpiceSharp.Simulations;
using SpiceSharpParser.ModelsReaders.Netlist.Spice;
using SpiceSharpParser.ModelsReaders.Netlist.Spice.Evaluation.CustomFunctions;
using SpiceSharpParser.ModelsReaders.Netlist.Spice.Readers.Controls.Plots;
using SpiceSharpRunner.Windows.Common;
using SpiceSharpRunner.Windows.Controls;
using SpiceSharpRunner.Windows.Logic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SpiceSharpRunner.Windows.ViewModels
{
    public class TabItem
    {
        public string Header { get; set; }

        public UIElement Content { get; set; }
    }

    public class TabsViewModel
    {
        public ObservableCollection<TabItem> Items { get; set; } = new ObservableCollection<TabItem>();
        public TabItem Selected { get; set; }
    }

    public class TreeItem
    {
        public UIElement Content { get; set; }
    }

    public class TreeViewModel
    {
        public ObservableCollection<TreeItem> Items { get; set; } = new ObservableCollection<TreeItem>();
    }

    public class NetlistResultWindowViewModel : ViewModelBase, IContent
    {
        public string Title { get; set; }

        public bool CanClose => true;

        public bool IsResizable => true;

        public string Netlist { get; set; }

        private TabsViewModel _plots;
        public TabsViewModel Plots
        {
            get
            {
                return _plots;
            }

            set
            {
                _plots = value;
                RaisePropertyChanged("Plots");
            }
        }

        private ObservableCollection<UIElement> _prints;
        public ObservableCollection<UIElement> Prints
        {
            get
            {
                return _prints;
            }

            set
            {
                _prints = value;
                RaisePropertyChanged("Prints");
            }
        }

        private TreeViewModel _internals;
        public TreeViewModel Internals
        {
            get
            {
                return _internals;
            }

            set
            {
                _internals = value;
                RaisePropertyChanged("Internals");
            }
        }

        private string _status;
        public string Status
        {
            get
            {
                return _status;
            }

            set
            {
                _status = value;
                RaisePropertyChanged("Status");
            }
        }

        private bool _plotsEnabled;
        public bool PlotsEnabled
        {
            get
            {
                return _plotsEnabled;
            }

            set
            {
                _plotsEnabled = value;
                RaisePropertyChanged("PlotsEnabled");
            }
        }

        private ObservableCollection<SimulationStatistics> _stats = new ObservableCollection<SimulationStatistics>();

        public ObservableCollection<SimulationStatistics> Stats
        {
            get
            {
                return _stats;
            }

            set
            {
                _stats = value;
                RaisePropertyChanged("Stats");
            }
        }

        private string _logs;
        public string Logs
        {
            get
            {
                return _logs;
            }

            set
            {
                _logs = value;
                RaisePropertyChanged("Logs");
            }
        }

        public Dispatcher Dispatcher { get; }
        public SpiceEvaluatorMode Mode { get; internal set; }

        public NetlistResultWindowViewModel(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        private void RunSimulations()
        {
            try
            {
                Status = "Status: Running simulations";
                PlotsEnabled = false;

                Internals = new TreeViewModel();
                Plots = new TabsViewModel();
                Prints = new ObservableCollection<UIElement>();

                var model = SpiceHelper.GetSpiceSharpNetlist(Netlist, Mode);

                Logs += $"Simulations found: {model.Simulations.Count}\n";

                int simulationNo = 1;
                foreach (BaseSimulation simulation in model.Simulations)
                {
                    RunSimulation(model, simulation, simulationNo);
                    simulationNo++;
                }

                // Generate plots
                if (model.Plots.Count > 0)
                {
                    PlotsEnabled = true;

                    Logs += $"Creating plots: {model.Plots.Count}\n";

                    if (model.Plots.Count > 0)
                    {
                        foreach (var plot in model.Plots)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                Plots.Items.Add(new TabItem() { Header = plot.Name, Content = new PlotControl() { Plot = plot } });
                            });
                        }
                    }
                }

                Logs += $"Prints found: {model.Prints.Count}\n";

                if (model.Prints.Count > 0)
                {
                    foreach (var print in model.Prints)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            PrintControl control = new PrintControl(print);
                            control.DataBind();
                            Prints.Add(control);
                        });
                    }
                }

                foreach (var warning in model.Warnings)
                {
                    Dispatcher.Invoke(() =>
                    {
                        Logs += ("Warning: " + warning + "\n");
                    });
                }

                Status = "Status: Finished";
            }
            catch (Exception ex)
            {
                Logs += ex.ToString();
                Status = "Status: Error";
            }
        }

        private void RunSimulation(SpiceNetlistReaderResult model, BaseSimulation simulation, int index)
        {
            // Setup for Internals tab
            simulation.FinalizeSimulationExport += (arg, e) => {
                Dispatcher.Invoke(() =>
                {
                    TreeViewItem simulationItem = new TreeViewItem() { Header = simulation.Name };
                    TreeViewItem objects = new TreeViewItem() { Header = "Objects" };
                    simulationItem.Items.Add(objects);

                    var enumerator = model.Circuit.Objects.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        var entity = enumerator.Current;
                        TreeViewItem item = new TreeViewItem() { Header = (string.Format("{0}     -    ({1})", entity.Name, entity)) };

                        if (entity is Component c)
                        {
                            for (var i = 0; i < c.PinCount; i++)
                            {
                                var nodeId = c.GetNode(i);
                                TreeViewItem nodeItem = new TreeViewItem() { Header = (string.Format("Node: {0}", nodeId)) };
                                item.Items.Add(nodeItem);
                            }
                        }

                        objects.Items.Add(item);
                    }

                    TreeViewItem variables = new TreeViewItem() { Header = "Variables" };
                    simulationItem.Items.Add(variables);

                    foreach (var variable in simulation.Nodes.GetVariables())
                    {
                        TreeViewItem item = new TreeViewItem { Header = string.Format("{0}     -     ({1})", variable.Name, variable.UnknownType) };
                        variables.Items.Add(item);
                    }

                    TreeViewItem parameters = new TreeViewItem() { Header = "Parameters" };
                    simulationItem.Items.Add(parameters);

                    foreach (var parameter in model.Evaluators[simulation].GetParameterNames())
                    {
                        TreeViewItem item = new TreeViewItem { Header = string.Format("{0}     -     ({1})", parameter, model.Evaluators[simulation].GetParameterValue(parameter, simulation)) };
                        parameters.Items.Add(item);
                    }

                    this.Internals.Items.Add(new TreeItem() { Content = simulationItem });
                });
            };
            var simulationStats = new SimulationStatistics()
            {
                SimulationNo = index,
                SimulationName = simulation.Name.ToString(),
            };

            simulation.Run(model.Circuit);

            simulationStats.Iterations = simulation.Statistics.Iterations;
            simulationStats.SolveTime = simulation.Statistics.SolveTime.ElapsedMilliseconds;
            simulationStats.LoadTime = simulation.Statistics.LoadTime.ElapsedMilliseconds;
            simulationStats.ReorderTime = simulation.Statistics.ReorderTime.ElapsedMilliseconds;
            simulationStats.BehaviorCreationTime = simulation.Statistics.BehaviorCreationTime.ElapsedMilliseconds;
            simulationStats.Timepoints = simulation.Statistics.TimePoints;
            simulationStats.TransientIterations = simulation.Statistics.TransientIterations;
            simulationStats.TransientTime = simulation.Statistics.TransientTime.ElapsedMilliseconds;
            simulationStats.AcceptedTimepoints = simulation.Statistics.Accepted;
            simulationStats.RejectedTimepoints = simulation.Statistics.Rejected;

            Dispatcher.Invoke(() =>
            {
                Stats.Add(simulationStats);
            });
        }

        /// <summary>
        /// Runs
        /// </summary>
        public void Run()
        {
            Task.Run(() =>
            {
                RunSimulations();
            });
        }
    }
}
