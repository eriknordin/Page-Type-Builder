﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EPiServer.Core.PropertySettings;
using EPiServer.DataAbstraction;
using EPiServer.Editor;
using PageTypeBuilder.Abstractions;
using PageTypeBuilder.Discovery;

namespace PageTypeBuilder.Synchronization
{
    public class PageTypePropertyUpdater
    {
        private ITabFactory _tabFactory;
        private IPropertySettingsRepository _propertySettingsRepository;

        public PageTypePropertyUpdater(
            IPageDefinitionFactory pageDefinitionFactory, 
            IPageDefinitionTypeFactory pageDefinitionTypeFactory, 
            ITabFactory tabFactory,
            IPropertySettingsRepository propertySettingsRepository)
        {
            PageDefinitionFactory = pageDefinitionFactory;
            PageDefinitionTypeFactory = pageDefinitionTypeFactory;
            PageTypePropertyDefinitionLocator = new PageTypePropertyDefinitionLocator();
            PageDefinitionTypeMapper = new PageDefinitionTypeMapper(PageDefinitionTypeFactory);
            _tabFactory = tabFactory;
            _propertySettingsRepository = propertySettingsRepository;
        }

        protected internal virtual void UpdatePageTypePropertyDefinitions(IPageType pageType, PageTypeDefinition pageTypeDefinition)
        {
            IEnumerable<PageTypePropertyDefinition> definitions = 
                PageTypePropertyDefinitionLocator.GetPageTypePropertyDefinitions(pageType, pageTypeDefinition.Type);

            foreach (PageTypePropertyDefinition propertyDefinition in definitions)
            {
                PageDefinition pageDefinition = GetExistingPageDefinition(pageType, propertyDefinition);
                if (pageDefinition == null)
                    pageDefinition = CreateNewPageDefinition(propertyDefinition);

                UpdatePageDefinition(pageDefinition, propertyDefinition);

                //Settings dev
                UpdatePropertySettings(pageTypeDefinition, propertyDefinition, pageDefinition);

                //End settings dev
            }
        }

        protected internal virtual void UpdatePropertySettings(PageTypeDefinition pageTypeDefinition, PageTypePropertyDefinition propertyDefinition, PageDefinition pageDefinition)
        {
            var prop =
                pageTypeDefinition.Type.GetProperties().Where(p => p.Name == propertyDefinition.Name).FirstOrDefault
                    ();
                
            var attributes = prop.GetCustomAttributes(typeof(PropertySettingsAttribute), true);
            foreach (var attribute in attributes)
            {
                PropertySettingsContainer container;

                if (pageDefinition.SettingsID == Guid.Empty)
                {
                    pageDefinition.SettingsID = Guid.NewGuid();
                    PageDefinitionFactory.Save(pageDefinition);
                    container = new PropertySettingsContainer(pageDefinition.SettingsID);
                }
                else
                {
                    if (!_propertySettingsRepository.TryGetContainer(pageDefinition.SettingsID, out container))
                    {
                        container = new PropertySettingsContainer(pageDefinition.SettingsID);
                    }
                }
                var settingsAttribute = (PropertySettingsAttribute) attribute;
                var wrapper = container.GetSetting(settingsAttribute.SettingType);
                if (wrapper == null)
                {
                    wrapper = new PropertySettingsWrapper();
                    container.Settings.Add(settingsAttribute.SettingType.FullName, wrapper);
                }

                bool settingsAlreadyExists = true;
                if (wrapper.PropertySettings == null)
                {
                    wrapper.PropertySettings = (IPropertySettings) Activator.CreateInstance(settingsAttribute.SettingType);
                    settingsAlreadyExists = false;
                }

                if(settingsAlreadyExists && !settingsAttribute.OverWriteExistingSettings)
                    return;

                if(settingsAttribute.UpdateSettings(wrapper.PropertySettings) || !settingsAlreadyExists)
                    _propertySettingsRepository.Save(container);
            }
        }

        protected internal virtual PageDefinition GetExistingPageDefinition(IPageType pageType, PageTypePropertyDefinition propertyDefinition)
        {
            return pageType.Definitions.FirstOrDefault(definition => definition.Name == propertyDefinition.Name);
        }

        protected internal virtual PageDefinition CreateNewPageDefinition(PageTypePropertyDefinition propertyDefinition)
        {
            PageDefinition pageDefinition = new PageDefinition();
            pageDefinition.PageTypeID = propertyDefinition.PageType.ID;
            pageDefinition.Name = propertyDefinition.Name;
            pageDefinition.EditCaption = propertyDefinition.GetEditCaptionOrName();
            SetPageDefinitionType(pageDefinition, propertyDefinition);

            PageDefinitionFactory.Save(pageDefinition);
            
            return pageDefinition;
        }

        protected internal virtual void SetPageDefinitionType(PageDefinition pageDefinition, PageTypePropertyDefinition propertyDefinition)
        {
            pageDefinition.Type = GetPageDefinitionType(propertyDefinition);
        }

        protected internal virtual PageDefinitionType GetPageDefinitionType(PageTypePropertyDefinition definition)
        {
            return PageDefinitionTypeMapper.GetPageDefinitionType(definition);
        }

        protected internal virtual void UpdatePageDefinition(PageDefinition pageDefinition, PageTypePropertyDefinition pageTypePropertyDefinition)
        {
            string oldValues = SerializeValues(pageDefinition);

            UpdatePageDefinitionValues(pageDefinition, pageTypePropertyDefinition);

            if(SerializeValues(pageDefinition) != oldValues)
                PageDefinitionFactory.Save(pageDefinition);
        }

        protected internal virtual string SerializeValues(PageDefinition pageDefinition)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(pageDefinition.EditCaption);
            builder.Append("|");
            builder.Append(pageDefinition.HelpText);
            builder.Append("|");
            builder.Append(pageDefinition.Required);
            builder.Append("|");
            builder.Append(pageDefinition.Searchable);
            builder.Append("|");
            builder.Append(pageDefinition.DefaultValue);
            builder.Append("|");
            builder.Append(pageDefinition.DefaultValueType);
            builder.Append("|");
            builder.Append(pageDefinition.LanguageSpecific);
            builder.Append("|");
            builder.Append(pageDefinition.DisplayEditUI);
            builder.Append("|");
            builder.Append(pageDefinition.FieldOrder);
            builder.Append("|");
            builder.Append(pageDefinition.LongStringSettings);
            builder.Append("|");
            builder.Append(pageDefinition.Tab.ID);
            builder.Append("|");

            return builder.ToString();
        }

        protected internal virtual void UpdatePageDefinitionValues(PageDefinition pageDefinition, PageTypePropertyDefinition pageTypePropertyDefinition)
        {
            PageTypePropertyAttribute propertyAttribute = pageTypePropertyDefinition.PageTypePropertyAttribute;

            pageDefinition.EditCaption = pageTypePropertyDefinition.GetEditCaptionOrName();
            pageDefinition.HelpText = propertyAttribute.HelpText ?? string.Empty;
            pageDefinition.Required = propertyAttribute.Required;
            pageDefinition.Searchable = propertyAttribute.Searchable;
            pageDefinition.DefaultValue = propertyAttribute.DefaultValue != null ? propertyAttribute.DefaultValue.ToString() : string.Empty;
            pageDefinition.DefaultValueType = propertyAttribute.DefaultValueType;
            pageDefinition.LanguageSpecific = propertyAttribute.UniqueValuePerLanguage;
            pageDefinition.DisplayEditUI = propertyAttribute.DisplayInEditMode;
            pageDefinition.FieldOrder = propertyAttribute.SortOrder;
            UpdateLongStringSettings(pageDefinition, propertyAttribute);
            UpdatePageDefinitionTab(pageDefinition, propertyAttribute);
        }

        protected internal virtual void UpdatePageDefinitionTab(PageDefinition pageDefinition, PageTypePropertyAttribute propertyAttribute)
        {
            TabDefinition tab = _tabFactory.List().First();
            if (propertyAttribute.Tab != null)
            {
                Tab definedTab = (Tab) Activator.CreateInstance(propertyAttribute.Tab);
                tab = _tabFactory.GetTabDefinition(definedTab.Name);
            }
            pageDefinition.Tab = tab;
        }

        private void UpdateLongStringSettings(PageDefinition pageDefinition, PageTypePropertyAttribute propertyAttribute)
        {
            EditorToolOption longStringSettings = propertyAttribute.LongStringSettings;
            if (longStringSettings == default(EditorToolOption) && !propertyAttribute.ClearAllLongStringSettings)
            {
                longStringSettings = EditorToolOption.All;
            }
            pageDefinition.LongStringSettings = longStringSettings;
        }

        internal PageTypePropertyDefinitionLocator PageTypePropertyDefinitionLocator { get; set; }

        internal IPageDefinitionFactory PageDefinitionFactory { get; set; }

        internal IPageDefinitionTypeFactory PageDefinitionTypeFactory { get; set; }

        internal PageDefinitionTypeMapper PageDefinitionTypeMapper { get; set; }
    }
}