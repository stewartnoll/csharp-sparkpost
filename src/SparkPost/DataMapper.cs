﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SparkPost
{
    public class DataMapper
    {
        public DataMapper(string version)
        {
            // sticking with v1 for now
        }

        public virtual IDictionary<string, object> ToDictionary(Transmission transmission)
        {
            return WithCommonConventions(transmission, new Dictionary<string, object>
            {
                ["metadata"] = transmission.Metadata.Count > 0 ? transmission.Metadata : null,
                ["substitution_data"] = transmission.SubstitutionData.Count > 0 ? transmission.SubstitutionData : null,
                ["recipients"] = BuildTheRecipientRequestFrom(transmission)
            });
        }

        public virtual IDictionary<string, object> ToDictionary(Recipient recipient)
        {
            return WithCommonConventions(recipient, new Dictionary<string, object>()
            {
                ["tags"] = recipient.Tags.Count > 0 ? recipient.Tags : null,
                ["metadata"] = recipient.Metadata.Count > 0 ? recipient.Metadata : null,
                ["substitution_data"] = recipient.SubstitutionData.Count > 0 ? recipient.SubstitutionData : null
            });
        }

        public virtual IDictionary<string, object> ToDictionary(Address address)
        {
            return WithCommonConventions(address);
        }

        public virtual IDictionary<string, object> ToDictionary(Options options)
        {
            if (typeof(Options)
                .GetProperties()
                .Any(x => x.GetValue(options) != null))
                return RemoveNulls(new Dictionary<string, object>
                {
                    ["start_time"] =
                        options.StartTime.HasValue ? string.Format("{0:s}{0:zzz}", options.StartTime.Value) : null,
                    ["open_tracking"] = options.OpenTracking.HasValue && options.OpenTracking.Value ? "true" : "false",
                    ["click_tracking"] =
                        options.ClickTracking.HasValue && options.ClickTracking.Value ? "true" : "false",
                    ["transactional"] = options.Transactional.HasValue && options.Transactional.Value ? "true" : "false",
                    ["sandbox"] = options.Sandbox.HasValue && options.Sandbox.Value ? "true" : "false",
                    ["skip_suppression"] =
                        options.SkipSuppression.HasValue && options.SkipSuppression.Value ? "true" : "false"
                });
            return null;
        }

        public virtual IDictionary<string, object> ToDictionary(Content content)
        {
            return WithCommonConventions(content, new Dictionary<string, object>
            {
                ["from"] = content.From.Email,
                ["attachments"] = content.Attachments.Any() ? content.Attachments.Select(ToDictionary) : null,
                ["inline_images"] = content.InlineImages.Any() ? content.InlineImages.Select(ToDictionary) : null,
                ["headers"] = content.Headers.Keys.Count > 0 ? content.Headers : null,
            });
        }

        public virtual IDictionary<string, object> ToDictionary(Attachment attachment)
        {
            return ToDictionary(attachment as File);
        }

        public virtual IDictionary<string, object> ToDictionary(InlineImage inlineImage)
        {
            return ToDictionary(inlineImage as File);
        }

        public virtual IDictionary<string, object> ToDictionary(File file)
        {
            return WithCommonConventions(file);
        }

        private object BuildTheRecipientRequestFrom(Transmission transmission)
        {

            return transmission.ListId != null
                ? (object)new Dictionary<string, object> { ["list_id"] = transmission.ListId }
                : transmission.Recipients.Select(ToDictionary);
        }

        private static IDictionary<string, object> RemoveNulls(IDictionary<string, object> dictionary)
        {
            var newDictionary = new Dictionary<string, object>();
            foreach (var key in dictionary.Keys.Where(k => dictionary[k] != null))
                newDictionary[key] = dictionary[key];
            return newDictionary;
        }

        private IDictionary<string, object> WithCommonConventions(object target, IDictionary<string, object> results = null)
        {
            var list = typeof (DataMapper).GetMethods()
                .Where(x => x.Name == "ToDictionary")
                .Where(x => x.GetParameters().Count() == 1)
                .Select(x => new {
                    TheType = x.GetParameters().First().ParameterType,
                    TheMethod = x
                }).ToList();
            var dictionary = list
                .ToDictionary(x=>x.TheType, x=>x.TheMethod);

            if (results == null) results = new Dictionary<string, object>();
            foreach (var property in target.GetType().GetProperties())
            {
                var name = ToSnakeCase(property.Name);
                if (results.ContainsKey(name) == false)
                {
                    var propertyType = property.PropertyType;
                    var o = property.GetValue(target);
                    if (dictionary.ContainsKey(propertyType))
                    {
                        var value = dictionary[propertyType].Invoke(this, BindingFlags.Default, null,
                            new[] {o}, CultureInfo.CurrentCulture);
                        results[name] = value;
                    }

                    if(results.ContainsKey(name) == false) results[name] = o;
                }
            }
            return RemoveNulls(results);
        }

        private string ToSnakeCase(string input)
        {
            var regex = new Regex("[A-Z]");

            var matches = regex.Matches(input);

            for (var i = 0; i < matches.Count; i++)
                input = input.Replace(matches[i].Value, "_" + matches[i].Value.ToLower());

            if (input.StartsWith("_"))
                input = input.Substring(1, input.Length - 1);

            return input;
        }
    }
}