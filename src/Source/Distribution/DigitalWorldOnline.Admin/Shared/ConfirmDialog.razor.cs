using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DigitalWorldOnline.Admin.Shared
{
    public partial class ConfirmDialog
    {
        [CascadingParameter]
        private MudDialogInstance MudDialog { get; set; }

        /// <summary>
        /// Dialog başlığı
        /// </summary>
        [Parameter]
        public string Title { get; set; }

        /// <summary>
        /// Dialog mesajı
        /// </summary>
        [Parameter]
        public string Message { get; set; }

        /// <summary>
        /// Dialog içerik metni (ContentText parametresi)
        /// </summary>
        [Parameter]
        public string ContentText { get; set; }  // ✅ EKLE

        /// <summary>
        /// Onay butonu yazısı
        /// </summary>
        [Parameter]
        public string ButtonText { get; set; } = "Confirm";

        /// <summary>
        /// Onay butonu rengi
        /// </summary>
        [Parameter]
        public Color Color { get; set; } = Color.Primary;

        void Submit() => MudDialog.Close(DialogResult.Ok(true));
        void Cancel() => MudDialog.Cancel();
    }
}