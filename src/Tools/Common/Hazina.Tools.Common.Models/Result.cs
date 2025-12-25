using Common.Models;
using System.Collections.Generic;

// Keep legacy namespace to minimize breaking changes across the codebase
namespace Common.Models.DTO
{
    public class Result<T>
    {
        public T Value { get; set; }
        public bool Success { get; set; } = false;
        public List<string> Errors { get; set; } = [];
        public Result() { }
    }
}

