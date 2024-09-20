using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceAPIV2.Models
{
    public class Folder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FolderId { get; set; }

        [Required, Display(Name = "Name")]
        public string FolderName { get; set; }

        [Required, Display(Name = "Path")]
        public string FolderPath { get; set; }

        public byte[]? Sheet { get; set; }

        public byte[]? FacesFolder { get; set; }

        public byte[]? VoicesFolder { get; set; }

        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("User")]
        public string User_Id { get; set; }
        public virtual User User { get; set; }

        [ForeignKey("ParentFolder")]
        public int? ParentFolderId { get; set; } // فولدر الأب، قد يكون null إذا كان هذا الفولدر في المستوى الأعلى
        public virtual Folder? ParentFolder { get; set; }

        //public ICollection<Folder>? SubFolders { get; set; } = new List<Folder>();// الفولدرات الفرعية
        public ICollection<Session>? Sessions { get; set; } = new List<Session>();// غرف الامتحان داخل هذا الفولدر    }
    }
}