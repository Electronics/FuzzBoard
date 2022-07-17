using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FuzzBoard {
	public partial class Form1 : Form {

		struct AudioItem {
			public DirectSoundOut Output;
			public AudioFileReader File;
		}

		public Form1() {
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e) {
			AudioItem audio;
			audio.File = new AudioFileReader(@"can_you_hear_me.wav");
			audio.Output = new DirectSoundOut(50);
			audio.Output.Init(audio.File);


			Button pauseButton = new Button();
			pauseButton.Text = "Pause";
			pauseButton.Click += (s, e2) => {
				//MessageBox.Show(this, ((ListViewItem)((Button)s).Tag).Index.ToString());
				if (audio.Output.PlaybackState==PlaybackState.Playing) {
					audio.Output.Pause();
				} else {
					audio.Output.Play();
				}
			};

			Button stopButton = new Button();
			stopButton.Text = "Stop";
			stopButton.Click += (s, e2) => {
				audio.Output.Stop();
			};

			var newItem = listView.Items.Add(new ListViewItem(new[] { $"New item {listView.Items.Count}", "Wow", "Does this work?" }));
			pauseButton.Tag = newItem; // so we can find the index of where the button is later on :)
			newItem.Tag = audio;
			listView.AddEmbeddedControl(pauseButton, 1, newItem.Index);
			listView.AddEmbeddedControl(stopButton, 2, newItem.Index);
		}

		private void button2_Click(object sender, EventArgs e) {
			if (listView.Items.Count == 0) return;
			listView.Items.Remove(listView.Items[0]);
		}

		private void button3_Click(object sender, EventArgs e) {
			listView.Items.Add(new ListViewItem(new[] { $"Boring {listView.Items.Count}", "Such", "Boring" }));
		}

		private void listView_MouseClick(object sender, MouseEventArgs e) {
			foreach(ListViewItem item in listView.Items) {
				var rectangle = item.GetBounds(ItemBoundsPortion.Entire);
				if (rectangle.Contains(e.Location)) {
					AudioItem audio = (AudioItem)item.Tag;
					if (audio.Output.PlaybackState == PlaybackState.Paused) audio.Output.Stop();
					audio.File.Position = 0;
					audio.Output.Play();
				}
			}
		}
	}
}
