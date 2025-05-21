namespace Altinn.Correspondence.Core.Models.Entities
{
    /// <summary>
    /// Class representing a person
    /// </summary>
    public class Person
    {
        /// <summary>
        /// Gets or sets the SSN
        /// </summary>
        public string? SSN { get; set; }
        
        /// <summary>
        /// Gets or sets the full name
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Gets or sets the first name
        /// </summary>
        public string? FirstName { get; set; }
        
        /// <summary>
        /// Gets or sets the middle name
        /// </summary>
        public string? MiddleName { get; set; }
        
        /// <summary>
        /// Gets or sets the last name
        /// </summary>
        public string? LastName { get; set; }
    }
}