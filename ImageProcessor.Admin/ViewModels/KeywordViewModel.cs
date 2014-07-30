using System.Windows;
using GalaSoft.MvvmLight;

namespace ImageProcessor.Admin.ViewModels
{
    public class KeywordViewModel : ViewModelBase
    {
        private readonly string _value;
        private int _postedCount;
        private double _fontSize;

        public KeywordViewModel(string value)
        {
            _value = value;
            PostedCount = 1;
        }

        public string Value
        {
            get { return _value; }
        }

        public int PostedCount
        {
            get { return _postedCount; }
            private set
            {
                Set(() => PostedCount, ref _postedCount, value);
                this.FontSize = SystemFonts.MessageFontSize + 4.0 * (value - 1);
            }
        }

        public double FontSize
        {
            get { return _fontSize; }
            private set { Set(() => FontSize, ref _fontSize, value); }
        }

        public void NotifyPosted()
        {
            PostedCount++;
        }
    }
}