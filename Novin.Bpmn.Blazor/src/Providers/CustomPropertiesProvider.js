import spellProps from './parts/SpellProps';
import numberFieldProps from './parts/NumberFieldProps';
import { is } from 'bpmn-js/lib/util/ModelUtil';

const LOW_PRIORITY = 500;

export default function CustomPropertiesProvider(propertiesPanel, translate) {
    this.getGroups = function(element) {
        console.log('ss')
        return function(groups) {
            // Add the "magic" group for StartEvent
            if (is(element, 'bpmn:StartEvent')) {
                groups.push(createMagicGroup(element, translate));
            }
            // Add the "service" group for ServiceTask
            if (is(element, 'bpmn:ServiceTask')) {
                groups.push(createServiceGroup(element, translate));
            }
            return groups;
        };
    };

    propertiesPanel.registerProvider(LOW_PRIORITY, this);
}

CustomPropertiesProvider.$inject = ['propertiesPanel', 'translate'];

function createMagicGroup(element, translate) {
    const magicGroup = {
        id: 'magic',
        label: translate('Magic properties'),
        entries: spellProps(element),
        tooltip: translate('Make sure you know what you are doing!')
    };
    return magicGroup;
}

function createServiceGroup(element, translate) {
    const serviceGroup = {
        id: 'service',
        label: translate('Implementation'),
        entries: numberFieldProps(element),
        tooltip: translate('Enter the details required for the service task.')
    };
    return serviceGroup;
}
