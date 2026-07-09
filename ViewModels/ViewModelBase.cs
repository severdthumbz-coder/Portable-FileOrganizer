using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Shared INotifyPropertyChanged plumbing for ViewModels.
    /// Implementation is identical to the one MainViewModel has always used, so behavior
    /// is unchanged; this simply gives the extracted feature ViewModels a common base.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
