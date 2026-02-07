using System;

namespace PhoneControlKit.Models
{
    /// <summary>
    /// Represents a message sent from the phone.
    /// </summary>
    public class PhoneMessage
    {
        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of when the message was sent.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the sender's identifier.
        /// </summary>
        public string SenderId { get; set; }

        /// <summary>
        /// Gets or sets the type of the message (e.g., text, image).
        /// </summary>
        public string MessageType { get; set; }
    }
}