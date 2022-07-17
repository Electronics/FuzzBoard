using FuzzySharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FuzzBoardWPF {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

        ObservableCollection<AudioItem> items = new ObservableCollection<AudioItem>();
        ObservableCollection<AudioItem> hotcueItems = new ObservableCollection<AudioItem>();

        public MainWindow() {
			InitializeComponent();

            audioList.ItemsSource = items;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(audioList.ItemsSource);
            view.Filter = UserFilter;

            // trial for drag and drop
            hotcues.ItemsSource = hotcueItems;

            discoverFiles();
        }
        private bool UserFilter(object item) {
            if (String.IsNullOrEmpty(txtFilter.Text)) {
                return true;
            } else {
                List<string> searchTerms = new List<string>();
                var numQuotes = Regex.Matches(txtFilter.Text, "\"").Count;
                if (numQuotes>1) {
                    // if there's an enclosed term
                    var s = txtFilter.Text.Split('\"');
                    for (int i = 0; i < numQuotes / 2; i++) {
                        var enclosedString = s[2*i + 1];

                        if (Array.FindIndex((item as AudioItem).SearchTags, s => Fuzz.TokenSortRatio(s,enclosedString) > 75) >= 0)
                            return true;

                        var notEnclosedString = s[2 * i];
						if (!string.IsNullOrWhiteSpace(notEnclosedString)) {
                            searchTerms.Add(notEnclosedString);
						}
                    }
                    if (!string.IsNullOrWhiteSpace(s[s.Length-1]))
                        searchTerms.Add(s[s.Length-1]);
				} else {
                    searchTerms.AddRange(txtFilter.Text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                }
                foreach (string searchTerm in searchTerms) {
                    if (Array.FindIndex((item as AudioItem).SearchTags, s => s.IndexOf(searchTerm,StringComparison.OrdinalIgnoreCase)>=0) >= 0)
                        return true;
				}
            }
            return false;
        }

        private void txtFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
            CollectionViewSource.GetDefaultView(audioList.ItemsSource).Refresh();
        }

        public void StopAll(object sender, RoutedEventArgs e) {
			Console.WriteLine($"Stopping all media players");
            foreach (AudioItem a in items) {
                a.Stop_();
			}
		}

        private void discoverFiles() {
            var dir = ".";
            var ext = new List<string> { "wav", "mp3", "ogg", "flac", "m4a" };
            var files = Directory
                .EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(s => ext.Contains(System.IO.Path.GetExtension(s).TrimStart('.').ToLowerInvariant()));

            foreach (var file in files) {
                var fileName = System.IO.Path.GetFileName(file);
                AudioItem a = new AudioItem() { FileName = fileName, Completion = 0 };
                a.Player.Open(new Uri(file, UriKind.Relative));

                List<string> searchTags = new List<string>();
                try {
                    var tfile = TagLib.File.Create(file);
                    if (!String.IsNullOrEmpty(tfile.Tag.Title))
                        searchTags.Add(tfile.Tag.Title);
                } catch (TagLib.CorruptFileException) {
					Console.WriteLine("Invalid ID3 tags on file - was it not an mp3?");
				}
                searchTags.AddRange(System.IO.Path.GetFileNameWithoutExtension(file).Split(new char[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries));
                a.SearchTags = searchTags.Distinct().ToArray();

                items.Add(a);
                Console.WriteLine($"Added audio item {file}, searchTags: {string.Join(", ", a.SearchTags)}");
            }

            CollectionViewSource.GetDefaultView(audioList.ItemsSource).Refresh();
        }

        private void audioListMouseMove(object sender, MouseEventArgs e) {
            Border b = sender as Border;
            if (b != null && e.LeftButton == MouseButtonState.Pressed) {
                // b.DataContext should be an AudioItem
                DragDrop.DoDragDrop(b, b.DataContext, DragDropEffects.Link);
			}
		}

        private void hotcuesDragOver(object sender, DragEventArgs e) {
            e.Effects = DragDropEffects.None;
            if (e.Data != null && e.Data.GetDataPresent(typeof(AudioItem))) {
                e.Effects = DragDropEffects.Link;
			}
            e.Handled = true;
		}
        private void hotcuesDrop(object sender, DragEventArgs e) {
            if (e.Data != null && e.Data.GetDataPresent(typeof(AudioItem))) {
                AudioItem a = (AudioItem)e.Data.GetData(typeof(AudioItem));
                if (!hotcueItems.Contains(a))
                    hotcueItems.Add(a);
            }
        }
    }

	public class AudioItem : INotifyPropertyChanged {
#nullable enable
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
#nullable disable

        public MediaPlayer Player = new MediaPlayer();
        private enum PlayState { Undefined, Stopped, Paused, Playing};
        private PlayState playState = PlayState.Undefined;

		public string FileName { get; set; }
        private int completion;
		public int Completion { get => completion; set => SetField(ref completion, value); }

        private string timeString;
        public string TimeString { get => timeString; set => SetField(ref timeString, value); }

        public string[] SearchTags { get; set; }

        private DispatcherTimer updateTimer = new DispatcherTimer(DispatcherPriority.DataBind);
        private DispatcherTimer resetTimer = new DispatcherTimer();

		public AudioItem() {
            updateTimer.Interval = TimeSpan.FromMilliseconds(50);
            updateTimer.Tick += UpdateCompletion;
            Player.MediaOpened += UpdateCompletion;
            resetTimer.Interval = TimeSpan.FromSeconds(30);
            resetTimer.Tick += Stop_;
        }

        private ICommand _playCommand;
		public ICommand Play { get {
                if (_playCommand == null) {
                    _playCommand = new RelayCommand(
                        param => this.Play_(),
                        param => true // Can play?
                    );
                }
                return _playCommand;
            } }
		public void Play_() {
            if (playState != PlayState.Paused)
                Player.Position = TimeSpan.Zero;
			Player.Play();
            playState = PlayState.Playing;
            updateTimer.Start();
            resetTimer.Stop();
		}

        private ICommand _pauseCommand;
        public ICommand Pause {
            get {
                if (_pauseCommand == null) {
                    _pauseCommand = new RelayCommand(
                        param => this.Pause_(),
                        param => true // Can play?
                    );
                }
                return _pauseCommand;
            }
        }
        public void Pause_() {
			Player.Pause();
            playState = PlayState.Paused;
            updateTimer.Stop();
            resetTimer.Start();
        }

        private ICommand _stopCommand;
        public ICommand Stop {
            get {
                if (_stopCommand == null) {
                    _stopCommand = new RelayCommand(
                        param => this.Stop_(),
                        param => true // Can play?
                    );
                }
                return _stopCommand;
            }
        }
        public void Stop_() {
			Player.Stop();
            playState = PlayState.Stopped;
            Player.Position = TimeSpan.Zero;
            updateTimer.Stop();
            UpdateCompletion(null, null); // send that last update

        }
        public void Stop_(object sender, EventArgs e) {
            Stop_();
        }

        private ICommand _deleteCommand;
        public ICommand Delete {
            get {
                if (_deleteCommand == null) {
                    _deleteCommand = new RelayCommand(
                        param => this.Delete_(param),
                        param => true // can delete
                        );
				}
                return _deleteCommand;
			}
		}
        public void Delete_(object o) { // delete this item off the hotcues list
            ObservableCollection<AudioItem> hotcues = o as ObservableCollection<AudioItem>;

            hotcues.Remove(this);
		}

        private ICommand _settingsCommand;
        public ICommand Settings {
            get {
                if (_settingsCommand == null) {
                    _settingsCommand = new RelayCommand(
                        param => this.Settings_(),
                        param => true
                        );
				}
                return _settingsCommand;
			}
		}
        public void Settings_() {
            ItemSettings settingsDialog = new ItemSettings(this);
            settingsDialog.Owner = Application.Current.MainWindow;
            settingsDialog.ShowDialog();
		}

        public void UpdateCompletion(object sender, EventArgs e) {
            try {
                Completion = (int)(Player.Position.TotalSeconds * 100 / Player.NaturalDuration.TimeSpan.TotalSeconds);
                TimeString = $"{Player.Position.ToString(@"mm\:ss")}/{Player.NaturalDuration.TimeSpan.ToString(@"mm\:ss")}";

                if (Completion >= 100) updateTimer.Stop();
            } catch (InvalidOperationException) {
				Console.WriteLine("Cannot get player position, is it an invalid file? - Stopping, should probably delete as well");
                updateTimer.Stop();
			}
        }
	}

    public static class CustomCommands {
        public static readonly RoutedUICommand Panic = new RoutedUICommand("StopAll", "StopAll", typeof(CustomCommands), new InputGestureCollection() {
            new KeyGesture(Key.Escape)
        });
	}

    public class RelayCommand : ICommand {
        #region Fields

        readonly Action<object> _execute;
        readonly Predicate<object> _canExecute;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// Creates a new command that can always execute.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
        public RelayCommand(Action<object> execute)
            : this(execute, null) {
        }

        /// <summary>
        /// Creates a new command.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
        /// <param name="canExecute">The execution status logic.</param>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute) {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion // Constructors

        #region ICommand Members

        [DebuggerStepThrough]
        public bool CanExecute(object parameters) {
            return _canExecute == null ? true : _canExecute(parameters);
        }

        public event EventHandler CanExecuteChanged {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameters) {
            _execute(parameters);
        }

        #endregion // ICommand Members
    }
}
