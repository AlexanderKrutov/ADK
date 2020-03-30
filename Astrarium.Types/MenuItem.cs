﻿using Astrarium.Types.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace Astrarium.Types
{
    public class MenuItem : ViewModelBase
    {       
        public MenuItem(string title)
        {
            this.Header = title;
            Text.LocaleChanged += () => NotifyPropertyChanged(nameof(Header));
        }

        public MenuItem(string title, ICommand command)
        {
            this.Header = title;
            this.Command = command;
            Text.LocaleChanged += () => NotifyPropertyChanged(nameof(Header));
        }

        public MenuItem(string title, ICommand command, object commandParameter)
        {
            this.Header = title;
            this.Command = command;
            this.CommandParameter = commandParameter;
            Text.LocaleChanged += () => NotifyPropertyChanged(nameof(Header));
        }

        public bool IsCheckable
        {
            get => GetValue<bool>(nameof(IsCheckable));
            set => SetValue(nameof(IsCheckable), value);
        }

        public bool IsChecked
        {
            get => GetValue<bool>(nameof(IsChecked));
            set => SetValue(nameof(IsChecked), value);
        }

        public bool IsEnabled
        {
            get => GetValue<bool>(nameof(IsEnabled), true);
            set => SetValue(nameof(IsEnabled), value);
        }

        public bool IsVisible
        {
            get => GetValue<bool>(nameof(IsVisible), true);
            set => SetValue(nameof(IsVisible), value);
        }

        public string Header
        {
            get => Text.Get(GetValue<string>(nameof(Header), null));
            set => SetValue(nameof(Header), value);
        }

        public string InputGestureText
        {
            get => GetValue<string>(nameof(InputGestureText), null);
            set => SetValue(nameof(InputGestureText), value);
        }

        public ICommand Command
        {
            get => GetValue<ICommand>(nameof(Command), null);
            set => SetValue(nameof(Command), value);
        }

        public object CommandParameter
        {
            get => GetValue<object>(nameof(CommandParameter), null);
            set => SetValue(nameof(CommandParameter), value);
        }

        public ObservableCollection<MenuItem> SubItems
        {
            get => GetValue<ObservableCollection<MenuItem>>(nameof(SubItems), null);
            set => SetValue(nameof(SubItems), value);
        }
    }
}
