using ControllerNode.Interfaces;
using ControllerNode.Services;   
using System.Threading;

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


    }
}
