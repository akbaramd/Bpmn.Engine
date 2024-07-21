import { html } from 'htm/preact';
import { TextFieldEntry, isTextFieldEntryEdited } from '@bpmn-io/properties-panel';
import { useService } from 'bpmn-js-properties-panel';

export default function(element) {
    return [
        {
            id: 'implementation',
            element,
            component: ImplementationField,
            isEdited: isTextFieldEntryEdited
        }
    ];
}

function ImplementationField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');

    const getValue = () => {
        return element.businessObject.implementation || '';
    };

    const setValue = value => {
        return modeling.updateProperties(element, {
            implementation: value
        });
    };

    return html`<${TextFieldEntry}
        id=${id}
        element=${element}
        description=${translate('Enter the implementation details')}
        label=${translate('Implementation')}
        getValue=${getValue}
        setValue=${setValue}
        debounce=${debounce}
        tooltip=${translate('Enter the implementation details for the service task.')}
    />`;
}
