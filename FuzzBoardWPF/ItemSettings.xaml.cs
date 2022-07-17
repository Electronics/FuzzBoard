using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FuzzBoardWPF {
	/// <summary>
	/// Interaction logic for ItemSettings.xaml
	/// </summary>
	public partial class ItemSettings : Window {
		public ItemSettings(AudioItem audioItem) {
			InitializeComponent();
			name.Content = audioItem.FileName;
		}
	}
}
