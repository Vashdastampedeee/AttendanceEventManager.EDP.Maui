using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace Attendance.Models
{
    [Table("event")]
    public class Event
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string EventName { get; set; }
        public string Category { get; set; }
        public byte[] ImageData { get; set; }
        public bool IsSelected { get; set; }
        public string EventDate { get; set; }  
        public string FromTime { get; set; }   
        public string ToTime { get; set; }     

        [Ignore] 
        public ImageSource ImageSource { get; set; }

        [Ignore]
        public bool IsActive => IsSelected;

    }
}
