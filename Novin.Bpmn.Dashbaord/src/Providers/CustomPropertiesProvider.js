import scriptTaskProps from './parts/ScriptTaskProps';
import formTaskProps from './parts/FormTaskProps';
import numberFieldProps from './parts/NumberFieldProps';
import assigneeProps from './parts/AssigneeProps';
import conditionScriptProps from './parts/ConditionScriptProps';
import { is } from 'bpmn-js/lib/util/ModelUtil';

const LOW_PRIORITY = 500;

export default function CustomPropertiesProvider(propertiesPanel, translate) {
    this.getGroups = function(element) {
        return function(groups) {
            // Add the "script" group for ScriptTask
            if (is(element, 'bpmn:ScriptTask')) {
                groups.push(createScriptGroup(element, translate));
            }
            // Add the "service" group for ServiceTask
            if (is(element, 'bpmn:ServiceTask')) {
                groups.push(createServiceGroup(element, translate));
            }
            // Add the "form" group for UserTask
            if (is(element, 'bpmn:UserTask')) {
                groups.push(createFormGroup(element, translate));
                groups.push(createAssigneeGroup(element, translate));
            }
            // Add the "condition" group for SequenceFlow originating from ExclusiveGateway or InclusiveGateway
            if (is(element, 'bpmn:SequenceFlow') && originatesFromGateway(element)) {
                groups.push(createConditionGroup(element, translate));
            }
            return groups;
        };
    };

    propertiesPanel.registerProvider(LOW_PRIORITY, this);
}

CustomPropertiesProvider.$inject = ['propertiesPanel', 'translate'];

function createScriptGroup(element, translate) {
    const scriptGroup = {
        id: 'script',
        label: translate('Script'),
        entries: scriptTaskProps(element),
        tooltip: translate('Enter the script to be executed.')
    };
    return scriptGroup;
}

function createServiceGroup(element, translate) {
    const serviceGroup = {
        id: 'service',
        label: translate('Properties'),
        entries: numberFieldProps(element),
        tooltip: translate('Enter the details required for the service task.')
    };
    return serviceGroup;
}

function createFormGroup(element, translate) {
    const formGroup = {
        id: 'form',
        label: translate('Form'),
        entries: formTaskProps(element),
        tooltip: translate('Enter the form ID.')
    };
    return formGroup;
}

function createAssigneeGroup(element, translate) {
    const assigneeGroup = {
        id: 'assignee',
        label: translate('Assignment'),
        entries: assigneeProps(element),
        tooltip: translate('Enter assignee and candidate groups.')
    };
    return assigneeGroup;
}

function createConditionGroup(element, translate) {
    const conditionGroup = {
        id: 'condition',
        label: translate('Condition Script'),
        entries: conditionScriptProps(element),
        tooltip: translate('Enter the condition script for the sequence flow.')
    };
    return conditionGroup;
}

function originatesFromGateway(element) {
    const source = element.source;

        return is(source, 'bpmn:ExclusiveGateway') || is(source, 'bpmn:InclusiveGateway');
}
