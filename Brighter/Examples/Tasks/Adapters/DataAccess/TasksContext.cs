using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Tasks.Model;

namespace Tasks.Adapters.DataAccess
{
    /// <summary>
    /// The entity framework context with a Task DbSet 
    /// </summary>
    public class TasksContext : DbContext
    {
        public TasksContext(DbContextOptions<TasksContext> options)
            : base(options)
        { }

        public DbSet<Task> Tasks { get; set; }

        public DbSet<MessageStore> MessageStores { get; set; }
     
    }

    //To create messagestore table
    public class MessageStore
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public Guid MessageId { get; set; }
        public string MessageType { get; set; }
        public string Topic { get; set; }
        public DateTime TimeStamp { get; set; }
        public string HeaderBag { get; set; }
        public string Body { get; set; }
    }
}