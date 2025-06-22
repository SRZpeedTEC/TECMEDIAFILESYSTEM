using ControllerNode.Interfaces;
using ControllerNode.Services;
using System.Threading;
using System.Net.Http.Json;

namespace ControllerNode
{
    public partial class Form1 : Form
    {
        private readonly ControllerService _controller;
        private IStorageNode[] nodes;
        private readonly Button[] buttons;

        public Form1(ControllerService controller)
        {
            InitializeComponent();
            _controller = controller;
            buttons = new Button[] { button1, button2, button3, button4 };
        }



        private void label1_Click(object sender, EventArgs e)
        {

        }

        protected override async void OnShown(EventArgs e)
        {
            nodes = _controller.GetStorageNodes();
            base.OnShown(e);
            await LoopAsync();
        }

        private async Task LoopAsync()
        {
            while (true)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    await UpdateNodeButtonAsync(nodes[i], buttons[i]);
                }

                await Task.Delay(100);
            }
        }

        private async Task UpdateNodeButtonAsync(IStorageNode node, Button button)
        {
            bool isOnline = await node.IsOnlineAsync(CancellationToken.None);

            button.Invoke((Action)(() =>
            {
                button.BackColor = isOnline ? Color.Green : Color.Red;
            }));
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private readonly HttpClient _http = new() { BaseAddress = new("http://localhost:6000") };



        private async void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                var status = await _http.GetFromJsonAsync<List<NodeDto>>("/status");
                // NodeDto = { id, online, nextIndex }
                foreach (var dto in status)
                {
                    Panel panel = Controls.Find($"pNode{dto.id}", true).First() as Panel;
                    panel.BackColor = dto.online ? Color.LightGreen : Color.LightCoral;
                }
            }
            catch { /* desconectado -> todo rojo */ }
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        record NodeDto(int id, bool online, long nextIndex);

    }
}
