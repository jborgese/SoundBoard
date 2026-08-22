#region Usings

using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

#endregion

namespace SoundBoard
{
    /// <summary>
    /// Interaction logic for ButtonGridDialog.xaml
    /// </summary>
    internal partial class ButtonGridDialog
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        public ButtonGridDialog()
        {
            InitializeComponent();

            RowUpDown.Value = DefaultRowCount;
            ColumnUpDown.Value = DefaultColumnCount;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="startingRowCount"></param>
        /// <param name="startingColumnCount"></param>
        public ButtonGridDialog(int startingRowCount, int startingColumnCount) : this()
        {
            RowCount = _startingRowCount = startingRowCount;
            ColumnCount = _startingColumnCount = startingColumnCount;
        }

        public ButtonGridDialog(int startingRowCount, int startingColumnCount, string title, bool validate) : this(startingRowCount, startingColumnCount)
        {
            Title = title;
            _validate = validate;
        }

        #endregion

        #region Public properties

        /// <summary>
        /// The result of the dialog
        /// </summary>
        public DialogResult DialogResult;

        /// <summary>
        /// Number of rows. Falls back to <see cref="DefaultRowCount"/> when the box has been emptied.
        /// </summary>
        public int RowCount
        {
            get => ToCount(RowUpDown.Value, DefaultRowCount);
            set => RowUpDown.Value = value;
        }

        /// <summary>
        /// Number of columns. Falls back to <see cref="DefaultColumnCount"/> when the box has been emptied.
        /// </summary>
        public int ColumnCount
        {
            get => ToCount(ColumnUpDown.Value, DefaultColumnCount);
            set => ColumnUpDown.Value = value;
        }

        #endregion

        #region Private constants

        /// <summary>
        /// Value used for the row count when the box is empty.
        /// </summary>
        private const int DefaultRowCount = 5;

        /// <summary>
        /// Value used for the column count when the box is empty.
        /// </summary>
        private const int DefaultColumnCount = 2;

        #endregion

        #region Private fields

        private readonly int _startingRowCount;

        private readonly int _startingColumnCount;

        #endregion

        #region Event handlers

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ColumnUpDown_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void RowUpDown_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void RowUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            ShowHideWarningLabel();
        }

        private void ColumnUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            ShowHideWarningLabel();
        }

        #endregion

        #region Private methods

        private void ShowHideWarningLabel()
        {
            if (_validate && WarningLabel is null == false)
            {
                WarningLabel.Visibility = RowCount < _startingRowCount || ColumnCount < _startingColumnCount
                        ? Visibility.Visible
                        : Visibility.Hidden;
            }
        }

        /// <summary>
        /// Converts a <see cref="MahApps.Metro.Controls.NumericUpDown"/> value (a nullable double) to a count,
        /// substituting <paramref name="defaultValue"/> when the box is empty.
        /// </summary>
        private static int ToCount(double? value, int defaultValue)
        {
            return value is double d ? Math.Max(1, (int)Math.Round(d)) : defaultValue;
        }

        private readonly bool _validate = true;

        #endregion
    }
}
