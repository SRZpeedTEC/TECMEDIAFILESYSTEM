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
            SuspendLayout();
            // 
            // txtBuscar
            // 
            txtBuscar.Location = new Point(30, 90);
            txtBuscar.Name = "txtBuscar";
            txtBuscar.Size = new Size(300, 31);
            txtBuscar.TabIndex = 1;
            txtBuscar.Text = "dd";
            txtBuscar.TextChanged += txtBuscar_TextChanged;
            // 
            // btnBuscar
            // 
            btnBuscar.BackColor = ColorTranslator.FromHtml("#A5D6A7");
            btnBuscar.ForeColor = Color.Black;

            btnBuscar.Location = new Point(350, 88);
            btnBuscar.Name = "btnBuscar";
            btnBuscar.Size = new Size(100, 34);
            btnBuscar.TabIndex = 2;
            btnBuscar.Text = "Buscar";
            btnBuscar.UseVisualStyleBackColor = false;
            btnBuscar.Click += btnBuscar_Click;
            // 
            // btnAgregar
            // 
            btnAgregar.BackColor = ColorTranslator.FromHtml("#42A5F5");
            btnAgregar.ForeColor = Color.White;
            btnAgregar.Location = new Point(650, 140);
            btnAgregar.Name = "btnAgregar";
            btnAgregar.Size = new Size(200, 50);
            btnAgregar.TabIndex = 5;
            btnAgregar.Text = "Agregar documento";
            btnAgregar.UseVisualStyleBackColor = false;
            btnAgregar.Click += btnAgregar_Click;
            // 
            // btnEliminar
            // 
            btnEliminar.BackColor = ColorTranslator.FromHtml("#A5D6A7");
            btnEliminar.ForeColor = Color.Black;

            btnEliminar.Location = new Point(650, 210);
            btnEliminar.Name = "btnEliminar";
            btnEliminar.Size = new Size(200, 50);
            btnEliminar.TabIndex = 6;
            btnEliminar.Text = "Eliminar documento";
            btnEliminar.UseVisualStyleBackColor = false;
            btnEliminar.Click += btnEliminar_Click;
            // 
            // btnDescargar
            // 
            btnDescargar.BackColor = ColorTranslator.FromHtml("#42A5F5");
            btnDescargar.ForeColor = Color.White;

            btnDescargar.Location = new Point(650, 280);
            btnDescargar.Name = "btnDescargar";
            btnDescargar.Size = new Size(200, 50);
            btnDescargar.TabIndex = 7;
            btnDescargar.Text = "Descargar documento";
            btnDescargar.UseVisualStyleBackColor = false;
            btnDescargar.Click += btnDescargar_Click;
            // 
            // btnRefrescar
            // 
            btnRefrescar.BackColor = ColorTranslator.FromHtml("#A5D6A7");
            btnRefrescar.ForeColor = Color.Black;
            btnRefrescar.Location = new Point(470, 88);
            btnRefrescar.Name = "btnRefrescar";
            btnRefrescar.Size = new Size(120, 34);
            btnRefrescar.TabIndex = 3;
            btnRefrescar.Text = "Refrescar lista";
            btnRefrescar.UseVisualStyleBackColor = false;
            btnRefrescar.Click += btnRefrescar_Click;
            // 
            // lstPdfs
            // 
            lstPdfs.BackColor = Color.White;
            lstPdfs.ForeColor = ColorTranslator.FromHtml("#263238");
            lstPdfs.FormattingEnabled = true;
            lstPdfs.ItemHeight = 25;
            lstPdfs.Location = new Point(30, 140);
            lstPdfs.Name = "lstPdfs";
            lstPdfs.RightToLeft = RightToLeft.No;
            lstPdfs.ScrollAlwaysVisible = true;
            lstPdfs.Size = new Size(600, 379);
            lstPdfs.TabIndex = 4;
            lstPdfs.SelectedIndexChanged += lstPdfs_SelectedIndexChanged;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.CausesValidation = false;
            lblTitulo.ForeColor = ColorTranslator.FromHtml("#0D47A1");
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.TextAlign = ContentAlignment.TopCenter;

            lblTitulo.Location = new Point(350, 9);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(203, 30);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "TEC Media System";
            lblTitulo.TextAlign = ContentAlignment.TopRight;
            lblTitulo.Click += lblTitulo_Click;
            // 
            // PDFapp
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = ColorTranslator.FromHtml("#E3F2FD");
            ClientSize = new Size(1026, 673);
            Controls.Add(lblTitulo);
            Controls.Add(txtBuscar);
            Controls.Add(btnBuscar);
            Controls.Add(btnRefrescar);
            Controls.Add(lstPdfs);
            Controls.Add(btnAgregar);
            Controls.Add(btnEliminar);
            Controls.Add(btnDescargar);
            Name = "PDFapp";
            Text = "PDFApp - TEC Media File System";
            Load += PDFapp_Load;
            ResumeLayout(false);
            PerformLayout();
        }


        #endregion
    }
}