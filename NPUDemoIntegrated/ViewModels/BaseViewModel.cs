using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
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
        public abstract string title { get; }
        public abstract string subTitle { get; }

        public ICommand ToggleMenuCommand { get; protected set; }
        public abstract ICommand ConnectCommand { get; }
        public abstract ICommand DisconnectCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }

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

            SaveCommand = new RelayCommand(param => {
                GlobalConfigManager.Instance.SaveConfig();
            });

            LoadCommand = new RelayCommand(param => {
                GlobalConfigManager.Instance.LoadConfig();
            });
        }

        public abstract void DeactivateModule(ModuleType targetModule);
        public abstract void Dispose();
    }
}
