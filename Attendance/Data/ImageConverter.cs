using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Attendance.Data
{
    public static class ImageConverter
    {
        public static ImageSource ConvertByteArrayToImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                return null;
            }
              
            return ImageSource.FromStream(() => new MemoryStream(imageData));
        }
    }
}
