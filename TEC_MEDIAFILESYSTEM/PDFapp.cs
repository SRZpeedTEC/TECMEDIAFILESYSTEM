using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http.Json;

namespace TEC_MEDIAFILESYSTEM
{
    public partial class PDFapp : Form
    {
        public PDFapp()
        {
            InitializeComponent();
        }

        private readonly HttpClient http = new() { BaseAddress = new("http://localhost:6000") };

        private async Task LoadListAsync(string? filtro = null)
        {
            var ep = filtro is null ? "/documents"
                                    : $"/documents?q={Uri.EscapeDataString(filtro)}";
            var lista = await http.GetFromJsonAsync<string[]>(ep);
            lstPdfs.DataSource = lista;
        }


        private async void PDFapp_Load(object sender, EventArgs e)
        {
            await LoadListAsync();
        }

        private void lstPdfs_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            if (lstPdfs.SelectedItem is not string name) return;
            var resp = await http.DeleteAsync($"/documents/{name}");
            MessageBox.Show(resp.IsSuccessStatusCode ? "Eliminado" : "Error");
            await LoadListAsync();
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e)
        {

        }

        private void lblTitulo_Click(object sender, EventArgs e)
        {

        }

        private async void btnBuscar_Click(object sender, EventArgs e)
        {
            await LoadListAsync(txtBuscar.Text);
        }

        private void btnRefrescar_Click(object sender, EventArgs e)
        {

        }
        private async void btnAgregar_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "PDF|*.pdf" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            string serverName = Path.GetFileName(dlg.FileName);   // con extensión
            if (lstPdfs.Items.Contains(serverName))
            {
                MessageBox.Show("Ya existe un documento con ese nombre.");
                return;
            }

            long total = new FileInfo(dlg.FileName).Length;
            pbUpload.Value = 0;
            var progress = new Progress<long>(b => pbUpload.Value = (int)(b * 100 / total));

            using var fs = File.OpenRead(dlg.FileName);
            using var stream = new ProgressStream(fs, progress);

            var content = new MultipartFormDataContent {
        { new StreamContent(stream), "file", serverName }
    };

            Console.WriteLine($"[Upload] {serverName} size={total}");
            Console.WriteLine($"[Upload] {Path.GetFileName(dlg.FileName)} size={total}");
            var resp = await http.PostAsync("/documents", content);
            MessageBox.Show(resp.IsSuccessStatusCode ? "Subido" : $"Error {resp.StatusCode}");
            await LoadListAsync();
            pbUpload.Value = 0;
        }



        private async void btnDescargar_Click(object sender, EventArgs e)
        {
            if (lstPdfs.SelectedItem is not string name) return;

            using var resp = await http.GetAsync($"/documents/{Uri.EscapeDataString(name)}");
            if (!resp.IsSuccessStatusCode) { MessageBox.Show("Error"); return; }


            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            using var sfd = new SaveFileDialog {FileName = $"{Path.GetFileNameWithoutExtension(name)}_{ts}.pdf",
                Filter = "PDF|*.pdf", OverwritePrompt = true, RestoreDirectory = true };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            File.WriteAllBytes(sfd.FileName, await resp.Content.ReadAsByteArrayAsync());
            MessageBox.Show("Descargado");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = sfd.FileName,
                UseShellExecute = true
            });

            Console.WriteLine($"[Download] saved → {sfd.FileName}");

        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }
    }
}
