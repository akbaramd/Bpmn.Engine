import { html } from 'htm/preact';
import { TextFieldEntry, isTextFieldEntryEdited } from '@bpmn-io/properties-panel';
import { useService } from 'bpmn-js-properties-panel';

export default function conditionScriptProps(element) {
    return [
        {
            id: 'conditionExpression',
            element,
            component: ConditionExpressionField,
            isEdited: isTextFieldEntryEdited
        }
    ];
}

function ConditionExpressionField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');
    const bpmnFactory = useService('bpmnFactory');

    const getValue = () => {
        const businessObject = element.businessObject;
        const conditionExpression = businessObject.get('conditionExpression');
        return conditionExpression ? conditionExpression.body : '';
    };

    const setValue = value => {
        const businessObject = element.businessObject;
        let conditionExpression = businessObject.get('conditionExpression');

        if (!conditionExpression) {
            conditionExpression = bpmnFactory.create('bpmn:FormalExpression', { body: value, language: 'bpmn2' });
            modeling.updateProperties(element, { conditionExpression });
        } else {
            conditionExpression.body = value;
            conditionExpression.language = 'bpmn2';
            modeling.updateProperties(element, { conditionExpression });
        }
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
