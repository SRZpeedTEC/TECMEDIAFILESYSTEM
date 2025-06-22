namespace TEC_MEDIAFILESYSTEM
{
    partial class PDFapp
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;     

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private TextBox txtBuscar;
        private Button btnBuscar;
        private Button btnAgregar;
        private Button btnEliminar;
        private Button btnDescargar;
        private Button btnRefrescar;
        private ListBox lstPdfs;
        private Label lblTitulo;

        private void InitializeComponent()
        {
            txtBuscar = new TextBox();
            btnBuscar = new Button();
            btnAgregar = new Button();
            btnEliminar = new Button();
            btnDescargar = new Button();
            btnRefrescar = new Button();
            lstPdfs = new ListBox();
            lblTitulo = new Label();
            pbUpload = new ProgressBar();
            pbDownload = new ProgressBar();
            label1 = new Label();
            label2 = new Label();
            SuspendLayout();
            // 
            // txtBuscar
            // 
            txtBuscar.Location = new Point(21, 54);
            txtBuscar.Margin = new Padding(2);
            txtBuscar.Name = "txtBuscar";
            txtBuscar.Size = new Size(211, 23);
            txtBuscar.TabIndex = 1;
            txtBuscar.Text = "dd";
            txtBuscar.TextChanged += txtBuscar_TextChanged;
            // 
            // btnBuscar
            // 
            btnBuscar.BackColor = Color.FromArgb(165, 214, 167);
            btnBuscar.ForeColor = Color.Black;
            btnBuscar.Location = new Point(245, 53);
            btnBuscar.Margin = new Padding(2);
            btnBuscar.Name = "btnBuscar";
            btnBuscar.Size = new Size(70, 20);
            btnBuscar.TabIndex = 2;
            btnBuscar.Text = "Buscar";
            btnBuscar.UseVisualStyleBackColor = false;
            btnBuscar.Click += btnBuscar_Click;
            // 
            // btnAgregar
            // 
            btnAgregar.BackColor = Color.FromArgb(66, 165, 245);
            btnAgregar.ForeColor = Color.White;
            btnAgregar.Location = new Point(455, 84);
            btnAgregar.Margin = new Padding(2);
            btnAgregar.Name = "btnAgregar";
            btnAgregar.Size = new Size(140, 30);
            btnAgregar.TabIndex = 5;
            btnAgregar.Text = "Agregar documento";
            btnAgregar.UseVisualStyleBackColor = false;
            btnAgregar.Click += btnAgregar_Click;
            // 
            // btnEliminar
            // 
            btnEliminar.BackColor = Color.FromArgb(165, 214, 167);
            btnEliminar.ForeColor = Color.Black;
            btnEliminar.Location = new Point(455, 126);
            btnEliminar.Margin = new Padding(2);
            btnEliminar.Name = "btnEliminar";
            btnEliminar.Size = new Size(140, 30);
            btnEliminar.TabIndex = 6;
            btnEliminar.Text = "Eliminar documento";
            btnEliminar.UseVisualStyleBackColor = false;
            btnEliminar.Click += btnEliminar_Click;
            // 
            // btnDescargar
            // 
            btnDescargar.BackColor = Color.FromArgb(66, 165, 245);
            btnDescargar.ForeColor = Color.White;
            btnDescargar.Location = new Point(455, 168);
            btnDescargar.Margin = new Padding(2);
            btnDescargar.Name = "btnDescargar";
            btnDescargar.Size = new Size(140, 30);
            btnDescargar.TabIndex = 7;
            btnDescargar.Text = "Descargar documento";
            btnDescargar.UseVisualStyleBackColor = false;
            btnDescargar.Click += btnDescargar_Click;
            // 
            // btnRefrescar
            // 
            btnRefrescar.BackColor = Color.FromArgb(165, 214, 167);
            btnRefrescar.ForeColor = Color.Black;
            btnRefrescar.Location = new Point(329, 53);
            btnRefrescar.Margin = new Padding(2);
            btnRefrescar.Name = "btnRefrescar";
            btnRefrescar.Size = new Size(84, 20);
            btnRefrescar.TabIndex = 3;
            btnRefrescar.Text = "Refrescar lista";
            btnRefrescar.UseVisualStyleBackColor = false;
            btnRefrescar.Click += btnRefrescar_Click;
            // 
            // lstPdfs
            // 
            lstPdfs.BackColor = Color.White;
            lstPdfs.ForeColor = Color.FromArgb(38, 50, 56);
            lstPdfs.FormattingEnabled = true;
            lstPdfs.ItemHeight = 15;
            lstPdfs.Location = new Point(21, 84);
            lstPdfs.Margin = new Padding(2);
            lstPdfs.Name = "lstPdfs";
            lstPdfs.RightToLeft = RightToLeft.No;
            lstPdfs.ScrollAlwaysVisible = true;
            lstPdfs.Size = new Size(421, 229);
            lstPdfs.TabIndex = 4;
            lstPdfs.SelectedIndexChanged += lstPdfs_SelectedIndexChanged;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.CausesValidation = false;
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.FromArgb(13, 71, 161);
            lblTitulo.Location = new Point(245, 5);
            lblTitulo.Margin = new Padding(2, 0, 2, 0);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(221, 32);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "TEC Media System";
            lblTitulo.TextAlign = ContentAlignment.TopRight;
            lblTitulo.Click += lblTitulo_Click;
            // 
            // pbUpload
            // 
            pbUpload.Location = new Point(21, 346);
            pbUpload.Name = "pbUpload";
            pbUpload.Size = new Size(421, 23);
            pbUpload.TabIndex = 8;
            pbUpload.Click += progressBar1_Click;
            // 
            // pbDownload
            // 
            pbDownload.Location = new Point(21, 408);
            pbDownload.Name = "pbDownload";
            pbDownload.Size = new Size(421, 23);
            pbDownload.TabIndex = 9;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(192, 328);
            label1.Name = "label1";
            label1.Size = new Size(102, 15);
            label1.TabIndex = 10;
            label1.Text = "Tiempo de subida";
            label1.Click += label1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(176, 390);
            label2.Name = "label2";
            label2.Size = new Size(114, 15);
            label2.TabIndex = 11;
            label2.Text = "Tiempo de descarga";
            // 
            // PDFapp
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(227, 242, 253);
            ClientSize = new Size(718, 466);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(pbDownload);
            Controls.Add(pbUpload);
            Controls.Add(lblTitulo);
            Controls.Add(txtBuscar);
            Controls.Add(btnBuscar);
            Controls.Add(btnRefrescar);
            Controls.Add(lstPdfs);
            Controls.Add(btnAgregar);
            Controls.Add(btnEliminar);
            Controls.Add(btnDescargar);
            Margin = new Padding(2);
            Name = "PDFapp";
            Text = "PDFApp - TEC Media File System";
            Load += PDFapp_Load;
            ResumeLayout(false);
            PerformLayout();
        }


        #endregion

        private ProgressBar pbUpload;
        private ProgressBar pbDownload;
        private Label label1;
        private Label label2;
    }
}