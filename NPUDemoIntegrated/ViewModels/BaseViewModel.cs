using NPUDemoIntegrated.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NPUDemoIntegrated.ViewModels
{
    abstract class BaseViewModel: Notifier, IDisposable
    {
        public ICommand ToggleMenuCommand { get; protected set; }
        public abstract ICommand ConnectCommand { get; }
        public abstract ICommand DisconnectCommand { get; }

        private ICommand _buttonCommand;
        public ICommand ButtonCommand
        {
            get => _buttonCommand;
            set { _buttonCommand = value; OnPropertyChanged(); }
        }

        private bool _is_menu_open = false;
        public bool is_menu_open
        {
            get => _is_menu_open;
            set { _is_menu_open = value; OnPropertyChanged(); }
        }

        protected BaseViewModel()
        {
            ToggleMenuCommand = new RelayCommand(param => {
                is_menu_open = !is_menu_open;
            });
        }
        public abstract void Dispose();
    }
}
