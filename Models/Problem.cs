﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Judge1.Models
{

    [NotMapped]
    public class TestCase
    {
        // When used as sample cases, Input and Output are contents.
        // When used as test cases, Input and Output are path to files.
        public string Input { get; set; }
        public string Output { get; set; }
    }

    public class Problem : ModelWithTimestamps
    {
        public int Id { get; set; }

        #region Problem Designer

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        #endregion

        #region Problem Description

        [Required] public string Name { get; set; }
        [Required, Column(TypeName = "text")] public string Description { get; set; }
        [Required, Column(TypeName = "text")] public string InputFormat { get; set; }
        [Required, Column(TypeName = "text")] public string OutputFormat { get; set; }
        [Column(TypeName = "text")] public string FootNote { get; set; }

        #endregion

        #region Judgement Protocol

        public double TimeLimit { get; set; }
        public double MemoryLimit { get; set; }

        public bool HasSpecialJudge { get; set; }
        [NotMapped] public Program SpecialJudgeProgram { get; set; }

        [Column("SpecialJudgeProgram", TypeName = "text")]
        public string SpecialJudgeProgramSerialized
        {
            get => JsonConvert.SerializeObject(SpecialJudgeProgram);
            set => SpecialJudgeProgram = string.IsNullOrEmpty(value)
                ? null
                : JsonConvert.DeserializeObject<Program>(value);
        }

        public bool HasHacking { get; set; }
        [NotMapped] public Program StandardProgram { get; set; }

        [Column("StandardProgram", TypeName = "text")]
        public string StandardProgramSerialized
        {
            get => JsonConvert.SerializeObject(StandardProgram);
            set => StandardProgram = string.IsNullOrEmpty(value)
                ? null
                : JsonConvert.DeserializeObject<Program>(value);
        }

        [NotMapped] public Program ValidatorProgram { get; set; }

        [Required, Column("ValidatorProgram", TypeName = "text")]
        public string ValidatorProgramSerialized
        {
            get => JsonConvert.SerializeObject(ValidatorProgram);
            set => ValidatorProgram = string.IsNullOrEmpty(value)
                ? null
                : JsonConvert.DeserializeObject<Program>(value);
        }

        [NotMapped] public List<TestCase> SampleCases;
        [NotMapped] public List<TestCase> TestCases;

        [Required, Column("SampleCases", TypeName = "text")]
        public string SampleCasesSerialized
        {
            get => JsonConvert.SerializeObject(SampleCases);
            set =>
                SampleCases = string.IsNullOrEmpty(value)
                    ? new List<TestCase>()
                    : JsonConvert.DeserializeObject<List<TestCase>>(value);
        }

        [Column("TestCases", TypeName = "text")]
        public string TestCasesSerialized
        {
            get => JsonConvert.SerializeObject(TestCases);
            set =>
                TestCases = string.IsNullOrEmpty(value)
                    ? new List<TestCase>()
                    : JsonConvert.DeserializeObject<List<TestCase>>(value);
        }

        #endregion
    }
}