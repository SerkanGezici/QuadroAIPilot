using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuadroAIPilot.Models.Web;

namespace QuadroAIPilot.Commands
{
    public partial class WebInfoCommand
    {
        /// <summary>
        /// Haberleri yeni olanları işaretleyerek formatlar
        /// </summary>
        private string FormatNewsWithNewIndicator(List<RSSItem> allItems, List<RSSItem> newItems)
        {
            var content = new StringBuilder();
            
            if (newItems != null && newItems.Any())
            {
                var newCount = newItems.Count;
                content.AppendLine($"🔔 **{(newCount == 1 ? "1 yeni haber" : $"{newCount} yeni haber")} var!**");
                content.AppendLine();
            }
            
            int index = 1;
            foreach (var item in allItems)
            {
                bool isNew = newItems != null && newItems.Any(n => n.Title == item.Title);
                
                // Yeni haberleri 🆕 ile işaretle
                if (isNew)
                {
                    content.AppendLine($"🆕 **{index}. {item.Title}**");
                }
                else
                {
                    content.AppendLine($"**{index}. {item.Title}**");
                }
                
                if (!string.IsNullOrEmpty(item.Description))
                {
                    content.AppendLine($"   {item.Description}");
                }
                
                if (!string.IsNullOrEmpty(item.Source))
                {
                    content.AppendLine($"   Kaynak: {item.Source} | {item.PublishDate:HH:mm}");
                }
                
                content.AppendLine();
                index++;
            }
            
            return content.ToString();
        }
    }
}