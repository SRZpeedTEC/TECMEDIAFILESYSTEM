using ControllerNode.Interfaces;
using ControllerNode.Services;   
using System.Threading;

namespace ControllerNode
{
    public partial class Form1 : Form
    {
        private readonly ControllerService _controller;
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
            base.OnShown(e);
            await LoopAsync();
        }

        private async Task LoopAsync()
        {
            // Demo: subir un PDF y recuperarlo
            byte[] pdf = File.ReadAllBytes("demo.pdf");
            await _controller.AddDocumentAsync("demo", pdf);
            Console.WriteLine("✔ Documento subido.");

            byte[]? reconstruido = await _controller.GetDocumentAsync("demo");
            File.WriteAllBytes("demo_recuperado.pdf", reconstruido!);
            Console.WriteLine("✔ Documento recuperado.");

            IStorageNode[] nodes = _controller.GetStorageNodes();

            while (true)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    IStorageNode node = nodes[i];
                    bool isOnline = await node.IsOnlineAsync(CancellationToken.None);
                    if (!isOnline)
                    {
                        buttons[i].Invoke((Action)(() =>
                        {
                            buttons[i].BackColor = Color.Red;
                        }));
                    }
                    else
                    {
                        buttons[i].Invoke((Action)(() =>
                        {
                            buttons[i].BackColor = Color.Green;
                        }));
                    }
                }

                await Task.Delay(500);
            }
        }

    }
}
