import { html } from 'htm/preact';
import { TextFieldEntry, isTextFieldEntryEdited } from '@bpmn-io/properties-panel';
import { useService } from 'bpmn-js-properties-panel';

export default function conditionScriptProps(element) {
    return [
        {
            id: 'conditionScript',
            element,
            component: ConditionScriptField,
            isEdited: isTextFieldEntryEdited
        },
        {
            id: 'conditionExpression',
            element,
            component: ConditionExpressionField,
            isEdited: isTextFieldEntryEdited
        }
    ];
}

function ConditionScriptField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');

    const getValue = () => {
        const businessObject = element.businessObject;
        return (businessObject.conditionScript && businessObject.conditionScript.body) || '';
    };

    const setValue = value => {
        const businessObject = element.businessObject;
        const conditionScript = businessObject.conditionScript || {};
        conditionScript.body = value;
        return modeling.updateProperties(element, {
            conditionScript
        });
    };

    return html`<${TextFieldEntry}
            id=${id}
            element=${element}
            description=${translate('Enter the condition script for the gateway')}
            label=${translate('Condition Script')}
            getValue=${getValue}
            setValue=${setValue}
            debounce=${debounce}
    />`;
}

function ConditionExpressionField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');

    const getValue = () => {
        const businessObject = element.businessObject;
        const conditionExpression = businessObject.conditionExpression;
        return conditionExpression ? conditionExpression.body : '';
    };

    const setValue = value => {
        const businessObject = element.businessObject;
        const conditionExpression = businessObject.conditionExpression || {};
        conditionExpression.body = value;
        conditionExpression.language = 'bpmn2'; // Ensure it's set to BPMN 2.0 standard
        return modeling.updateProperties(element, {
            conditionExpression
        });
    };

    return html`<${TextFieldEntry}
            id=${id}
            element=${element}
            description=${translate('Enter the condition expression for the flow')}
            label=${translate('Condition Expression')}
            getValue=${getValue}
            setValue=${setValue}
            debounce=${debounce}
    />`;
}
