using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SoundBoard.Model
{
    /// <summary>
    /// Minimal <see cref="INotifyPropertyChanged"/> base for model classes.
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Assigns <paramref name="value"/> to <paramref name="field"/> and raises <see cref="PropertyChanged"/> if it changed.
        /// </summary>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raises <see cref="PropertyChanged"/>.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
