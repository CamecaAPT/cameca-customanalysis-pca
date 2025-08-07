#pragma once

namespace Cameca::CustomAnalysis::PcaLib::Interface {

    template<class T>
    public ref class ManagedWrapper abstract

    {
    public:
        ManagedWrapper(T* instance)
            : m_Instance(instance)
        {
        }
        virtual ~ManagedWrapper()
        {
            if (m_Instance != nullptr)
            {
                delete m_Instance;
            }
        }
        !ManagedWrapper()
        {
            if (m_Instance != nullptr)
            {
                delete m_Instance;
            }
        }

        T* GetInstance()
        {
            return m_Instance;
        }
    private:
        T* m_Instance;
    };
}