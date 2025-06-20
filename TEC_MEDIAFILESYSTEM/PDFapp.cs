using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TEC_MEDIAFILESYSTEM
{
    public partial class PDFapp : Form
    {
        public PDFapp()
        {
            InitializeComponent();
        }

        private void PDFapp_Load(object sender, EventArgs e)
        {

        }

        private void lstPdfs_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void btnEliminar_Click(object sender, EventArgs e)
        {

        }

        private void txtBuscar_TextChanged(object sender, EventArgs e)
        {

        }

        private void lblTitulo_Click(object sender, EventArgs e)
        {

        }

        private void btnBuscar_Click(object sender, EventArgs e)
        {

        }

        private void btnRefrescar_Click(object sender, EventArgs e)
        {

        }
        private async void btnAgregar_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "PDF Files (*.pdf)|*.pdf";
                openFileDialog.Title = "Seleccionar PDF";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    string fileName = Path.GetFileName(filePath);
                    byte[] fileBytes = File.ReadAllBytes(filePath);

                    using (var client = new HttpClient())
                    {
                        var requestContent = new MultipartFormDataContent();
                        var byteContent = new ByteArrayContent(fileBytes);
                        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

                        requestContent.Add(byteContent, "file", fileName);

                        try
                        {
                            // Ajusta esta URL si tus compañeros usaron otro endpoint
                            string controllerUrl = "http://localhost:5000/api/documentos/agregar";

                            var response = await client.PostAsync(controllerUrl, requestContent);

                            if (response.IsSuccessStatusCode)
                            {
                                MessageBox.Show("Documento agregado con éxito.");
                            }
                            else
                            {
                                MessageBox.Show("Error al agregar el documento. Código: " + response.StatusCode);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error al conectar con el servidor: " + ex.Message);
                        }
                    }
                }
            }
        }
        private void btnDescargar_Click(object sender, EventArgs e)
        {

        }
    }
}
