using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageProcessor.Admin.ViewModels
{
    public partial class KeywordViewModel : ObservableObject
    {
        private readonly string _value;

        [ObservableProperty]
        private int _postedCount;

        [ObservableProperty]
        private double _fontSize;

        public KeywordViewModel(string value)
        {
            _value = value;
            PostedCount = 1;
        }

        public string Value => _value;

        partial void OnPostedCountChanged(int value)
        {
            FontSize = SystemFonts.MessageFontSize + 4.0 * (value - 1);
        }

        public void NotifyPosted()
        {
            PostedCount++;
        }
    }
}
