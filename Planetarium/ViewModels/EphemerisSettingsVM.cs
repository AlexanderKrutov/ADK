﻿using Planetarium.Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planetarium.ViewModels
{
    public class EphemerisSettingsVM : ViewModelBase
    {
        private readonly Sky sky;

        public ObservableCollection<Node> Nodes { get; private set; } = new ObservableCollection<Node>();

        public Command OkCommand { get; private set; }
        public Command CancelCommand { get; private set; }

        private CelestialObject _SelectedBody = null;
        public CelestialObject SelectedBody
        {
            get
            {
                return _SelectedBody;
            }
            set
            {
                _SelectedBody = value;
                BuildCategoriesTree();
                NotifyPropertyChanged(nameof(SelectedBody));
                NotifyPropertyChanged(nameof(OkButtonEnabled));
            }
        }

        public IEnumerable<string> Categories
        {
            get
            {
                return
                    AllNodes(Nodes.First())
                        .Where(n => n.IsChecked ?? false)
                        .Select(n => n.Text);
            }
        }

        public double JulianDayFrom { get; set; }
        public double JulianDayTo { get; set; }
        public double Step { get; set; } = 1;

        private IEnumerable<Node> AllNodes(Node node)
        {
            yield return node;

            foreach (Node child in node.Children)
            {
                foreach (Node n in AllNodes(child))
                {
                    yield return n;
                }
            }
        }

        public bool OkButtonEnabled
        {
            get
            {
                return Nodes.Any() && Nodes.First().IsChecked != false;
            }
        }

        public EphemerisSettingsVM(Sky sky)
        {
            this.sky = sky;

            OkCommand = new Command(Ok);
            CancelCommand = new Command(Close);
        }

        public void Ok()
        {           
            Close(true);
        }

        private void BuildCategoriesTree()
        {
            Nodes.Clear();

            if (SelectedBody != null)
            {
                var categories = sky.GetEphemerisCategories(SelectedBody);

                var groups = categories.GroupBy(cat => cat.Split('.').First());

                Node root = new Node() { Text = "All" };
                root.CheckedChanged += Root_CheckedChanged;

                foreach (var group in groups)
                {
                    Node node = new Node() { Text = group.Key };

                    if (group.Count() > 1)
                    {
                        foreach (var item in group)
                        {
                            node.Children.Add(new Node() { Text = item });
                        }
                    }

                    root.Children.Add(node);
                }

                Nodes.Add(root);
            }
        }

        private void Root_CheckedChanged(object sender, bool? e)
        {
            NotifyPropertyChanged(nameof(OkButtonEnabled));
        }
    }
}